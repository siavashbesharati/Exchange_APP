using ForexExchange.Services.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace ForexExchange.Services.Notifications.Providers
{
    /// <summary>
    /// SignalR notification provider
    /// ارائه‌دهنده اعلان SignalR
    /// </summary>
    public class SignalRNotificationProvider : INotificationProvider
    {
        private readonly IHubContext<ForexExchange.Hubs.NotificationHub> _hubContext;
        private readonly ILogger<SignalRNotificationProvider> _logger;
        private readonly IConfiguration _configuration;

        public string ProviderName => "SignalR";

        public bool IsEnabled => _configuration.GetValue<bool>("Notifications:SignalR:Enabled", true);

        public SignalRNotificationProvider(
            IHubContext<ForexExchange.Hubs.NotificationHub> hubContext,
            ILogger<SignalRNotificationProvider> logger,
            IConfiguration configuration)
        {
            _hubContext = hubContext;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendOrderNotificationAsync(NotificationContext context)
        {
            try
            {
                var notificationData = new
                {
                    id = Guid.NewGuid().ToString(),
                    title = context.Title,
                    message = context.Message,
                    type = "order",
                    eventType = context.EventType.ToString(),
                    data = new Dictionary<string, object>(context.Data)
                    {
                        ["excludeUserIds"] = context.ExcludeUserIds
                    },
                    url = context.NavigationUrl,
                    priority = context.Priority.ToString(),
                    timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("SignalR order notification data: Title={Title}, URL={Url}", context.Title, context.NavigationUrl);

                if (context.SendToAllAdmins)
                {
                    // Send to all admins - client-side filtering will handle user exclusion
                    await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", notificationData);
                    
                    if (context.ExcludeUserIds.Any())
                    {
                        _logger.LogDebug("SignalR order notification sent to all admins (client-side will exclude {ExcludeCount} users): {Title}", context.ExcludeUserIds.Count, context.Title);
                    }
                    else
                    {
                        _logger.LogDebug("SignalR order notification sent to all admins: {Title}", context.Title);
                    }
                }

                if (context.TargetUserIds.Any())
                {
                    foreach (var userId in context.TargetUserIds)
                    {
                        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notificationData);
                    }
                    _logger.LogDebug("SignalR order notification sent to {Count} specific users: {Title}", context.TargetUserIds.Count, context.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR order notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendAccountingDocumentNotificationAsync(NotificationContext context)
        {
            try
            {
                var notificationData = new
                {
                    id = Guid.NewGuid().ToString(),
                    title = context.Title,
                    message = context.Message,
                    type = "document",
                    eventType = context.EventType.ToString(),
                    data = new Dictionary<string, object>(context.Data)
                    {
                        ["excludeUserIds"] = context.ExcludeUserIds
                    },
                    url = context.NavigationUrl,
                    priority = context.Priority.ToString(),
                    timestamp = DateTime.UtcNow
                };

                if (context.SendToAllAdmins)
                {
                    // Send to all admins - client-side filtering will handle user exclusion
                    await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", notificationData);
                    
                    if (context.ExcludeUserIds.Any())
                    {
                        _logger.LogDebug("SignalR document notification sent to all admins (client-side will exclude {ExcludeCount} users): {Title}", context.ExcludeUserIds.Count, context.Title);
                    }
                    else
                    {
                        _logger.LogDebug("SignalR document notification sent to all admins: {Title}", context.Title);
                    }
                }

                if (context.TargetUserIds.Any())
                {
                    foreach (var userId in context.TargetUserIds)
                    {
                        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notificationData);
                    }
                    _logger.LogDebug("SignalR document notification sent to {Count} specific users: {Title}", context.TargetUserIds.Count, context.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR document notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendCustomerNotificationAsync(NotificationContext context)
        {
            try
            {
                var notificationData = new
                {
                    id = Guid.NewGuid().ToString(),
                    title = context.Title,
                    message = context.Message,
                    type = "customer",
                    eventType = context.EventType.ToString(),
                    data = new Dictionary<string, object>(context.Data)
                    {
                        ["excludeUserIds"] = context.ExcludeUserIds
                    },
                    url = context.NavigationUrl,
                    priority = context.Priority.ToString(),
                    timestamp = DateTime.UtcNow
                };

                if (context.SendToAllAdmins)
                {
                    // Send to all admins - client-side filtering will handle user exclusion
                    await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", notificationData);
                    
                    if (context.ExcludeUserIds.Any())
                    {
                        _logger.LogDebug("SignalR customer notification sent to all admins (client-side will exclude {ExcludeCount} users): {Title}", context.ExcludeUserIds.Count, context.Title);
                    }
                    else
                    {
                        _logger.LogDebug("SignalR customer notification sent to all admins: {Title}", context.Title);
                    }
                }

                if (context.TargetUserIds.Any())
                {
                    foreach (var userId in context.TargetUserIds)
                    {
                        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notificationData);
                    }
                    _logger.LogDebug("SignalR customer notification sent to {Count} specific users: {Title}", context.TargetUserIds.Count, context.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR customer notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendSystemNotificationAsync(NotificationContext context)
        {
            try
            {
                var notificationData = new
                {
                    id = Guid.NewGuid().ToString(),
                    title = context.Title,
                    message = context.Message,
                    type = "system",
                    eventType = context.EventType.ToString(),
                    data = new Dictionary<string, object>(context.Data)
                    {
                        ["excludeUserIds"] = context.ExcludeUserIds
                    },
                    url = context.NavigationUrl,
                    priority = context.Priority.ToString(),
                    timestamp = DateTime.UtcNow
                };

                if (context.SendToAllAdmins)
                {
                    // Send to all admins - client-side filtering will handle user exclusion
                    await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", notificationData);
                    
                    if (context.ExcludeUserIds.Any())
                    {
                        _logger.LogDebug("SignalR system notification sent to all admins (client-side will exclude {ExcludeCount} users): {Title}", context.ExcludeUserIds.Count, context.Title);
                    }
                    else
                    {
                        _logger.LogDebug("SignalR system notification sent to all admins: {Title}", context.Title);
                    }
                }

                if (context.TargetUserIds.Any())
                {
                    foreach (var userId in context.TargetUserIds)
                    {
                        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notificationData);
                    }
                    _logger.LogDebug("SignalR system notification sent to {Count} specific users: {Title}", context.TargetUserIds.Count, context.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR system notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendManualAdjustmentNotificationAsync(NotificationContext context)
        {
            try
            {
                var notificationData = new
                {
                    id = Guid.NewGuid().ToString(),
                    title = context.Title,
                    message = context.Message,
                    type = "custom",
                    eventType = context.EventType.ToString(),
                    data = new Dictionary<string, object>(context.Data)
                    {
                        ["excludeUserIds"] = context.ExcludeUserIds
                    },
                    url = context.NavigationUrl,
                    priority = context.Priority.ToString(),
                    timestamp = DateTime.UtcNow
                };

                if (context.SendToAllAdmins)
                {
                    // Send to all admins - client-side filtering will handle user exclusion
                    await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", notificationData);
                    
                    if (context.ExcludeUserIds.Any())
                    {
                        _logger.LogDebug("SignalR custom notification sent to all admins (client-side will exclude {ExcludeCount} users): {Title}", context.ExcludeUserIds.Count, context.Title);
                    }
                    else
                    {
                        _logger.LogDebug("SignalR custom notification sent to all admins: {Title}", context.Title);
                    }
                }

                if (context.TargetUserIds.Any())
                {
                    foreach (var userId in context.TargetUserIds)
                    {
                        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notificationData);
                    }
                    _logger.LogDebug("SignalR custom notification sent to {Count} specific users: {Title}", context.TargetUserIds.Count, context.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR custom notification: {Title}", context.Title);
                throw;
            }
        }
    }
}
