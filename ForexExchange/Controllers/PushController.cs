using ForexExchange.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Text.Json;
using WebPush;
using Microsoft.Extensions.Configuration;

namespace ForexExchange.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PushController : ControllerBase
    {
        private const bool WebPushEnabled = true;
        private readonly ForexDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PushController> _logger;
        private readonly IVapidService _vapidService;
        private readonly WebPushClient _webPushClient;

        public PushController(
            ForexDbContext context,
            IConfiguration configuration,
            ILogger<PushController> logger,
            IVapidService vapidService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _vapidService = vapidService;

            // Initialize WebPush client
            _webPushClient = new WebPushClient();
        }

        /// <summary>
        /// Initialize VAPID details for WebPush client
        /// راه‌اندازی جزئیات VAPID برای کلاینت WebPush
        /// </summary>
        private async Task InitializeVapidAsync()
        {
            try
            {
                var vapidDetails = await _vapidService.GetVapidDetailsAsync();
                _webPushClient.SetVapidDetails(vapidDetails.Subject, vapidDetails.PublicKey, vapidDetails.PrivateKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing VAPID details");
                throw;
            }
        }

        /// <summary>
        /// Get VAPID public key for client subscription
        /// </summary>
        [HttpGet("publickey")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicKey()
        {
            if (!WebPushEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push notifications are currently disabled" });
            }

            try
            {
                var vapidDetails = await _vapidService.GetVapidDetailsAsync();
                return Ok(new { publicKey = vapidDetails.PublicKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting VAPID public key");
                return StatusCode(500, new { error = "Failed to get public key" });
            }
        }

        /// <summary>
        /// Subscribe to push notifications
        /// </summary>
        [HttpPost("subscribe")]
        [Authorize]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request)
        {
            if (!WebPushEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push notifications are currently disabled" });
            }

            string? userId = null;
            try
            {
                // Use ClaimTypes.NameIdentifier to get the actual user ID instead of username
                userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Push subscription request from user: {UserId}", userId);
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Push subscription request with no user identity");
                    return Unauthorized(new { error = "User not authenticated" });
                }

                // Validate request
                if (request?.Subscription?.Endpoint == null || 
                    request.Subscription.Keys?.P256dh == null || 
                    request.Subscription.Keys?.Auth == null)
                {
                    _logger.LogWarning("Invalid subscription request from user {UserId}", userId);
                    return BadRequest(new { error = "Invalid subscription data" });
                }

                _logger.LogInformation("Subscription data: Endpoint={Endpoint}, P256dh length={P256dhLength}, Auth length={AuthLength}", 
                    request.Subscription.Endpoint, 
                    request.Subscription.Keys.P256dh?.Length ?? 0,
                    request.Subscription.Keys.Auth?.Length ?? 0);

                // Validate that the user exists in the database
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogWarning("User {UserId} not found in database", userId);
                    return BadRequest(new { error = "User not found" });
                }

                // Check if subscription already exists
                var existingSubscription = await _context.PushSubscriptions
                    .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == request.Subscription.Endpoint);

                if (existingSubscription != null)
                {
                    // Update existing subscription
                    existingSubscription.P256dhKey = request.Subscription.Keys.P256dh!;
                    existingSubscription.AuthKey = request.Subscription.Keys.Auth!;
                    existingSubscription.IsActive = true;
                    existingSubscription.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new subscription
                    var subscription = new Models.PushSubscription
                    {
                        UserId = userId,
                        Endpoint = request.Subscription.Endpoint,
                        P256dhKey = request.Subscription.Keys.P256dh!,
                        AuthKey = request.Subscription.Keys.Auth!,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.PushSubscriptions.Add(subscription);
                    _logger.LogInformation("Creating new push subscription for user {UserId} with endpoint {Endpoint}", 
                        userId, request.Subscription.Endpoint);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Push subscription created/updated successfully for user {UserId}", userId);
                return Ok(new { message = "Subscription saved successfully", userId = userId });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("FOREIGN KEY constraint failed") == true)
            {
                _logger.LogError(dbEx, "Foreign key constraint failed when subscribing user {UserId}", userId);
                return BadRequest(new { error = "Invalid user reference", details = dbEx.InnerException?.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to push notifications for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to save subscription", details = ex.Message });
            }
        }

        /// <summary>
        /// Unsubscribe from push notifications
        /// </summary>
        [HttpPost("unsubscribe")]
        [Authorize]
        public async Task<IActionResult> Unsubscribe([FromBody] PushSubscriptionRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var subscription = await _context.PushSubscriptions
                    .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Endpoint == request.Subscription.Endpoint);

                if (subscription != null)
                {
                    subscription.IsActive = false;
                    subscription.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Push subscription deactivated for user {UserId}", userId);
                }

                var responseMessage = WebPushEnabled
                    ? "Unsubscribed successfully"
                    : "Push notifications are disabled; subscription deactivated if it existed.";

                return Ok(new { message = responseMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from push notifications");
                return StatusCode(500, new { error = "Failed to unsubscribe" });
            }
        }

        /// <summary>
        /// Check subscription status
        /// </summary>
        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            if (!WebPushEnabled)
            {
                return Ok(new { isSubscribed = false, hasActiveSubscriptions = false, message = "Push notifications are disabled" });
            }

            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var hasActiveSubscription = await _context.PushSubscriptions
                    .AnyAsync(ps => ps.UserId == userId && ps.IsActive);

                return Ok(new { isSubscribed = hasActiveSubscription, hasActiveSubscriptions = hasActiveSubscription });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking subscription status");
                return StatusCode(500, new { error = "Failed to check status" });
            }
        }

        /// <summary>
        /// Send test notification to current user
        /// </summary>
        [HttpPost("test")]
        [Authorize] // Temporarily allow any authenticated user for debugging
        public async Task<IActionResult> SendTestNotification()
        {
            if (!WebPushEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push notifications are currently disabled" });
            }

            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Test notification request from user: {UserId}", userId);
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Test notification request with no user identity");
                    return Unauthorized(new { error = "User not authenticated" });
                }

                // Get user's subscriptions
                var subscriptions = await _context.PushSubscriptions
                    .Where(ps => ps.UserId == userId && ps.IsActive)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} active subscriptions for user {UserId}", subscriptions.Count, userId);

                if (!subscriptions.Any())
                {
                    return BadRequest(new { error = "No active subscriptions found", userId = userId });
                }

                // Initialize VAPID
                try
                {
                    await InitializeVapidAsync();
                    _logger.LogInformation("VAPID initialized successfully");
                }
                catch (Exception vapidEx)
                {
                    _logger.LogError(vapidEx, "Failed to initialize VAPID");
                    return StatusCode(500, new { error = "VAPID initialization failed", details = vapidEx.Message });
                }

                var payload = JsonSerializer.Serialize(new
                {
                    title = "🧪 تست اعلان | Test Notification",
                    body = "این یک پیام تست است | This is a test message",
                    icon = "/icon-192x192.png",
                    badge = "/badge-72x72.png",
                    tag = "test-notification",
                    data = new
                    {
                        type = "test",
                        timestamp = DateTime.UtcNow,
                        url = "/admin"
                    }
                });

                var successCount = 0;
                var errorCount = 0;

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        var webPushSubscription = new WebPush.PushSubscription(
                            subscription.Endpoint,
                            subscription.P256dhKey,
                            subscription.AuthKey);

                        await _webPushClient.SendNotificationAsync(webPushSubscription, payload);
                        successCount++;

                        // Update subscription stats
                        subscription.SuccessfulNotifications++;
                        subscription.LastNotificationSent = DateTime.UtcNow;
                    }
                    catch (WebPushException ex)
                    {
                        _logger.LogWarning(ex, "Failed to send test notification to endpoint {Endpoint}", subscription.Endpoint);
                        errorCount++;

                        // Update subscription stats
                        subscription.FailedNotifications++;

                        // Deactivate subscription if permanently failed
                        if (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            subscription.IsActive = false;
                        }
                    }

                    subscription.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Test notifications sent",
                    successCount,
                    errorCount,
                    totalSubscriptions = subscriptions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, new { error = "Failed to send test notification" });
            }
        }

        /// <summary>
        /// Get push notification statistics
        /// </summary>
        [HttpGet("stats")]
        
        public async Task<IActionResult> GetPushStats()
        {
            if (!WebPushEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push notifications are currently disabled" });
            }

            try
            {
                var stats = new
                {
                    totalSubscriptions = await _context.PushSubscriptions.CountAsync(),
                    activeSubscriptions = await _context.PushSubscriptions.CountAsync(ps => ps.IsActive),
                    totalNotificationsSent = await _context.PushNotificationLogs.CountAsync(),
                    successfulNotifications = await _context.PushNotificationLogs.CountAsync(pnl => pnl.WasSuccessful),
                    failedNotifications = await _context.PushNotificationLogs.CountAsync(pnl => !pnl.WasSuccessful),
                    notificationsToday = await _context.PushNotificationLogs
                        .CountAsync(pnl => pnl.SentAt.Date == DateTime.UtcNow.Date),
                    vapidStats = await _vapidService.GetVapidStatisticsAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting push notification statistics");
                return StatusCode(500, new { error = "Failed to get statistics" });
            }
        }

        /// <summary>
        /// Send test notification with specific URL to current user
        /// ارسال اعلان تست با آدرس مشخص به کاربر فعلی
        /// </summary>
        [HttpPost("test-url")]
        [Authorize]
        public async Task<IActionResult> SendTestNotificationWithUrl([FromBody] TestUrlRequest request)
        {
            if (!WebPushEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push notifications are currently disabled" });
            }

            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Test URL notification request from user: {UserId} with URL: {Url}", userId, request.Url);

                // Create test push notification with explicit URL
                var payload = JsonSerializer.Serialize(new
                {
                    title = $"🧪 تست آدرس | URL Test",
                    body = $"تست هدایت به: {request.Url}",
                    icon = "/favicon/apple-touch-icon.png",
                    badge = "/favicon/favicon-32x32.png",
                    tag = "test-url-notification",
                    data = new
                    {
                        type = "test-url",
                        eventType = "Custom",
                        priority = "Normal",
                        timestamp = DateTime.UtcNow,
                        url = request.Url, // Explicit URL for testing
                        contextData = new { testUrl = request.Url }
                    }
                });

                // Get user subscriptions and send test notification
                var subscriptions = await _context.PushSubscriptions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .ToListAsync();

                foreach (var subscription in subscriptions)
                {
                    await InitializeVapidAsync();
                    var pushSubscription = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dhKey, subscription.AuthKey);
                    await _webPushClient.SendNotificationAsync(pushSubscription, payload);
                }

                return Ok(new { 
                    success = true,
                    message = $"Test URL notification sent to {subscriptions.Count} subscription(s)",
                    testUrl = request.Url
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test URL notification");
                return StatusCode(500, new { error = "Failed to send test URL notification" });
            }
        }
    }

    /// <summary>
    /// Request model for push subscription
    /// </summary>
    public class PushSubscriptionRequest
    {
        public PushSubscriptionData Subscription { get; set; } = new();
    }

    /// <summary>
    /// Push subscription data model
    /// </summary>
    public class PushSubscriptionData
    {
        public string Endpoint { get; set; } = string.Empty;
        public PushSubscriptionKeys Keys { get; set; } = new();
    }

    /// <summary>
    /// Push subscription keys model
    /// </summary>
    public class PushSubscriptionKeys
    {
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test URL request model
    /// </summary>
    public class TestUrlRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}
