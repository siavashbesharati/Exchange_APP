using ForexExchange.Models;
using ForexExchange.Services.Notifications.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services.Notifications
{
    /// <summary>
    /// Central notification hub interface
    /// رابط مرکز اعلان‌های مرکزی
    /// </summary>
    public interface INotificationHub
    {
        /// <summary>
        /// Send notification for order events
        /// ارسال اعلان برای رویدادهای معامله
        /// </summary>
        Task SendOrderNotificationAsync(
            Order order,
            NotificationEventType eventType,
            string? userId = null,
            string? oldStatus = null,
            string? newStatus = null
        );

        /// <summary>
        /// Send notification for accounting document events
        /// ارسال اعلان برای رویدادهای سند حسابداری
        /// </summary>
        Task SendAccountingDocumentNotificationAsync(
            AccountingDocument document,
            NotificationEventType eventType,
            string? userId = null
        );

        /// <summary>
        /// Send notification for customer events
        /// ارسال اعلان برای رویدادهای مشتری
        /// </summary>
        Task SendCustomerNotificationAsync(
            Customer customer,
            NotificationEventType eventType,
            string? userId = null
        );

        Task SendTaskNotificationAsync(
            TaskItem task,
            NotificationEventType eventType,
            string? userId = null,
            string? oldStatus = null
        );

        Task SendSystemNotificationAsync(
            string title,
            string message,
            NotificationEventType eventType = NotificationEventType.SystemError,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.High,
            Dictionary<string, object>? data = null
        );

        /// <summary>
        /// Send custom notification
        /// ارسال اعلان معاملهی
        /// </summary>
        Task SendManualAdjustmentNotificationAsync(
            string title,
            string message,
            NotificationEventType eventType = NotificationEventType.ManualAdjustment,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.Normal
        );


        /// <summary>
        /// Send custom notification
        /// ارسال اعلان معاملهی
        /// </summary>
        Task SendCustomeNotificationAsync(
            string title,
            string message,
            NotificationEventType eventType = NotificationEventType.PublicEvent,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.Low
        );

        Task SendPublicNotificationAsync(
            string title,
            string message,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.Low,
            Dictionary<string, object>? data = null
        );




        /// <summary>
        /// Register a notification provider
        /// ثبت ارائه‌دهنده اعلان
        /// </summary>
        void RegisterProvider(INotificationProvider provider);

        /// <summary>
        /// Get all registered providers
        /// دریافت همه ارائه‌دهندگان ثبت شده
        /// </summary>
        IEnumerable<INotificationProvider> GetProviders();

        /// <summary>
        /// Enable or disable a notification provider
        /// فعال یا غیرفعال کردن ارائه‌دهنده اعلان
        /// </summary>
        Task SetProviderEnabledAsync(string providerName, bool enabled);
    }

    /// <summary>
    /// Central notification hub implementation
    /// پیاده‌سازی مرکز اعلان‌های مرکزی
    /// </summary>
    public class NotificationHub : INotificationHub
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<NotificationHub> _logger;
        private readonly List<INotificationProvider> _providers;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationHub(
            ForexDbContext context,
            ILogger<NotificationHub> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager
        )
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _providers = new List<INotificationProvider>();
        }

        public void RegisterProvider(INotificationProvider provider)
        {
            if (!_providers.Any(p => p.ProviderName == provider.ProviderName))
            {
                _providers.Add(provider);
                _logger.LogInformation(
                    "Registered notification provider: {ProviderName}",
                    provider.ProviderName
                );
            }
        }

        public IEnumerable<INotificationProvider> GetProviders() => _providers.AsReadOnly();

        /// <summary>
        /// Check whether notifications are disabled for the current environment
        /// بررسی فعال بودن اعلان‌ها در محیط فعلی
        /// </summary>
        private bool ShouldSkipNotification()
        {
            var enabledInDevelopment = _configuration.GetValue(
                "Notifications:EnabledInDevelopment",
                true
            );
            return _environment.IsDevelopment() && !enabledInDevelopment;
        }

        public Task SetProviderEnabledAsync(string providerName, bool enabled)
        {
            // This could be stored in database settings
            var settingKey = $"Notifications:{providerName}:Enabled";

            // For now, we'll just log it. In the future, implement database settings
            _logger.LogInformation(
                "Provider {ProviderName} enabled status changed to: {Enabled}",
                providerName,
                enabled
            );

            // TODO: Store in SystemSettings table
            // await _settingsService.UpdateSettingAsync(settingKey, enabled.ToString());

            return Task.CompletedTask;
        }

        public async Task SendOrderNotificationAsync(
            Order order,
            NotificationEventType eventType,
            string? userId = null,
            string? oldStatus = null,
            string? newStatus = null
        )
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug(
                    "Skipping order notification in development mode for order {OrderId}, event {EventType}",
                    order.Id,
                    eventType
                );
                return;
            }

            try
            {
                var context = await BuildOrderNotificationContextAsync(
                    order,
                    eventType,
                    userId,
                    oldStatus,
                    newStatus
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendOrderNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending order notification for order {OrderId}, event {EventType}",
                    order.Id,
                    eventType
                );
            }
        }

        public async Task SendAccountingDocumentNotificationAsync(
            AccountingDocument document,
            NotificationEventType eventType,
            string? userId = null
        )
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug(
                    "Skipping document notification in development mode for document {DocumentId}, event {EventType}",
                    document.Id,
                    eventType
                );
                return;
            }

            try
            {
                var context = await BuildAccountingDocumentNotificationContextAsync(
                    document,
                    eventType,
                    userId
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendAccountingDocumentNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                // Idempotent: Log error but don't throw - notification failures should not affect main operations
                _logger.LogError(
                    ex,
                    "Error sending accounting document notification for document {DocumentId}, event {EventType}. Notification failed but document operation succeeded. ExceptionType: {ExceptionType}",
                    document.Id,
                    eventType,
                    ex.GetType().Name
                );

                if (ex.InnerException != null)
                {
                    _logger.LogError(
                        "Inner Exception: {InnerExceptionType} - {InnerExceptionMessage}",
                        ex.InnerException.GetType().Name,
                        ex.InnerException.Message
                    );
                }

                // Don't throw - this is idempotent
            }
        }

        public async Task SendCustomerNotificationAsync(
            Customer customer,
            NotificationEventType eventType,
            string? userId = null
        )
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug(
                    "Skipping customer notification in development mode for customer {CustomerId}, event {EventType}",
                    customer.Id,
                    eventType
                );
                return;
            }

            try
            {
                var context = await BuildCustomerNotificationContextAsync(
                    customer,
                    eventType,
                    userId
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendCustomerNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending customer notification for customer {CustomerId}, event {EventType}",
                    customer.Id,
                    eventType
                );
            }
        }

        public async Task SendTaskNotificationAsync(
            TaskItem task,
            NotificationEventType eventType,
            string? userId = null,
            string? oldStatus = null
        )
        {
            if (ShouldSkipNotification())
                return;

            try
            {
                var context = await BuildTaskNotificationContextAsync(
                    task,
                    eventType,
                    userId,
                    oldStatus
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendSystemNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending task notification for task {TaskId}, event {EventType}",
                    task.Id,
                    eventType
                );
            }
        }

        public async Task SendSystemNotificationAsync(
            string title,
            string message,
            NotificationEventType eventType = NotificationEventType.SystemError,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.High,
            Dictionary<string, object>? data = null
        )
        {
            if (ShouldSkipNotification())
                return;

            try
            {
                var context = BuildGenericNotificationContext(
                    title,
                    message,
                    eventType,
                    userId,
                    navigationUrl,
                    priority,
                    data
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendSystemNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system notification: {Title}", title);
            }
        }

        public async Task SendManualAdjustmentNotificationAsync(
            string title,
            string message,
            NotificationEventType eventType = NotificationEventType.ManualAdjustment,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.Normal
        )
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug(
                    "Skipping custom notification in development mode: {Title}",
                    title
                );
                return;
            }

            try
            {
                var context = await BuildManualAdjustmentNotificationContextAsync(
                    title,
                    message,
                    eventType,
                    userId,
                    navigationUrl,
                    priority
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendManualAdjustmentNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending custom notification: {Title}", title);
            }
        }



        public Task SendCustomeNotificationAsync(
            string title,
            string message,
            NotificationEventType eventType = NotificationEventType.PublicEvent,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.Low
        )
        {
            return SendPublicNotificationAsync(
                title,
                message,
                userId,
                navigationUrl,
                priority
            );
        }

        public async Task SendPublicNotificationAsync(
            string title,
            string message,
            string? userId = null,
            string? navigationUrl = null,
            NotificationPriority priority = NotificationPriority.Low,
            Dictionary<string, object>? data = null
        )
        {
            if (ShouldSkipNotification())
                return;

            try
            {
                var context = BuildGenericNotificationContext(
                    title,
                    message,
                    NotificationEventType.PublicEvent,
                    userId,
                    navigationUrl,
                    priority,
                    data
                );
                await EnrichWithActorInfoAsync(context);
                await SendNotificationToProvidersAsync(
                    context,
                    provider => provider.SendSystemNotificationAsync(context)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending public notification: {Title}", title);
            }
        }

        private async Task SendNotificationToProvidersAsync(
            NotificationContext context,
            Func<INotificationProvider, Task> sendAction
        )
        {
            var enabledProviders = _providers.Where(p => p.IsEnabled).ToList();
            _logger.LogInformation(
                "All enabled notification providers: {Providers}",
                string.Join(",", enabledProviders)
            );

            _logger.LogInformation(
                "All  notification providers: {Providers}",
                string.Join(",", _providers)
            );

            if (!enabledProviders.Any())
            {
                _logger.LogWarning(
                    "No enabled notification providers found for event {EventType}",
                    context.EventType
                );
                return;
            }

            var tasks = enabledProviders.Select(async provider =>
            {
                try
                {
                    await sendAction(provider);
                    _logger.LogDebug(
                        "Notification sent via {ProviderName} for event {EventType}",
                        provider.ProviderName,
                        context.EventType
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error sending notification via {ProviderName} for event {EventType}",
                        provider.ProviderName,
                        context.EventType
                    );
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task EnrichWithActorInfoAsync(NotificationContext context)
        {
            context.OccurredAt = DateTime.Now;
            context.Actor.UserName = "سیستم";
            var httpContext = _httpContextAccessor.HttpContext;

            if (!string.IsNullOrWhiteSpace(httpContext?.User.Identity?.Name))
                context.Actor.UserName = httpContext.User.Identity.Name;

            if (!string.IsNullOrEmpty(context.UserId))
            {
                try
                {
                    var user = await _userManager.FindByIdAsync(context.UserId);
                    context.Actor.UserName = !string.IsNullOrWhiteSpace(user?.FullName)
                        ? user!.FullName
                        : user?.UserName ?? context.Actor.UserName;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not resolve notification actor {UserId}",
                        context.UserId
                    );
                }
            }

            if (httpContext == null)
                return;

            context.Actor.IpAddress = ClientRequestInfoHelper.GetClientIpAddress(httpContext);
            context.Actor.UserAgent = httpContext.Request.Headers.UserAgent.ToString();
            context.Actor.Browser = ClientRequestInfoHelper.ParseBrowser(context.Actor.UserAgent);
            context.Actor.OperatingSystem = ClientRequestInfoHelper.ParseOperatingSystem(
                context.Actor.UserAgent
            );
        }

        private async Task<NotificationContext> BuildTaskNotificationContextAsync(
            TaskItem task,
            NotificationEventType eventType,
            string? userId,
            string? oldStatus
        )
        {
            var assignedUser = task.AssignedToUser;
            if (assignedUser == null && !string.IsNullOrEmpty(task.AssignedToUserId))
            {
                assignedUser = await _userManager.FindByIdAsync(task.AssignedToUserId);
            }

            var title = eventType switch
            {
                NotificationEventType.TaskAssignment => "📌 وظیفه واگذار شد",
                NotificationEventType.TaskDueReminder => "⏰ یادآوری سررسید وظیفه",
                NotificationEventType.TaskOverdue => "🚨 وظیفه سررسید گذشته",
                NotificationEventType.TaskCompleted => "✅ وظیفه تکمیل شد",
                _ => "🔄 وضعیت وظیفه تغییر کرد",
            };

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = task.Description,
                NavigationUrl = $"/Tasks/Details/{task.Id}",
                Priority = eventType == NotificationEventType.TaskOverdue
                    ? NotificationPriority.High
                    : NotificationPriority.Normal,
                SendToAllAdmins = true,
                ExcludeUserIds = !string.IsNullOrEmpty(userId)
                    ? new List<string> { userId }
                    : new List<string>(),
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "Task",
                    EntityId = task.Id,
                },
                Data = new Dictionary<string, object>
                {
                    ["taskId"] = task.Id,
                    ["title"] = task.Title,
                    ["description"] = task.Description,
                    ["assignedTo"] = assignedUser?.FullName
                        ?? assignedUser?.UserName
                        ?? "بدون مسئول",
                    ["dueDate"] = task.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "بدون سررسید",
                    ["oldStatus"] = oldStatus ?? "",
                    ["status"] = task.Status.ToString(),
                },
            };
        }

        private static NotificationContext BuildGenericNotificationContext(
            string title,
            string message,
            NotificationEventType eventType,
            string? userId,
            string? navigationUrl,
            NotificationPriority priority,
            Dictionary<string, object>? data
        )
        {
            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = priority,
                SendToAllAdmins = true,
                ExcludeUserIds = !string.IsNullOrEmpty(userId)
                    ? new List<string> { userId }
                    : new List<string>(),
                Data = data ?? new Dictionary<string, object>(),
            };
        }

        private async Task<NotificationContext> BuildOrderNotificationContextAsync(
            Order order,
            NotificationEventType eventType,
            string? userId,
            string? oldStatus,
            string? newStatus
        )
        {
            var customer = await _context.Customers.FindAsync(order.CustomerId);
            var fromCurrency = await _context.Currencies.FindAsync(order.FromCurrencyId);
            var toCurrency = await _context.Currencies.FindAsync(order.ToCurrencyId);

            var title = eventType switch
            {
                NotificationEventType.OrderCreated => "🔔 معامله جدید ثبت شد",
                NotificationEventType.OrderDeleted => "❌ معامله حذف شد",
                _ => "📋 رویداد معامله",
            };

            var message = eventType switch
            {
                NotificationEventType.OrderCreated =>
                    $"معامله #{order.Id} برای {customer?.FullName ?? "نامعلوم"}: {order.FromAmount:N0} {fromCurrency?.PersianName} → {order.ToAmount:N0} {toCurrency?.PersianName}",
                NotificationEventType.OrderDeleted =>
                    $"معامله #{order.Id} برای {customer?.FullName ?? "نامعلوم"} لغو شد",
                _ => $"رویداد معامله #{order.Id}",
            };

            var navigationUrl = $"/Orders/Details/{order.Id}";
            _logger.LogInformation(
                "Order notification URL generated: {NavigationUrl} for order {OrderId}",
                navigationUrl,
                order.Id
            );

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = NotificationPriority.Normal,
                SendToAllAdmins = true, // Always send to all admins
                ExcludeUserIds = !string.IsNullOrEmpty(userId)
                    ? new List<string> { userId }
                    : new List<string>(),
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "Order",
                    EntityId = order.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["customerId"] = order.CustomerId,
                        ["customerName"] = customer?.FullName ?? "نامعلوم",
                        ["fromCurrencyId"] = order.FromCurrencyId,
                        ["toCurrencyId"] = order.ToCurrencyId,
                        ["amount"] = order.FromAmount,
                        ["totalAmount"] = order.ToAmount,
                        ["rate"] = order.Rate,
                    },
                },
                Data = new Dictionary<string, object>
                {
                    ["orderId"] = order.Id,
                    ["customerId"] = order.CustomerId,
                    ["customerName"] = customer?.FullName ?? "نامعلوم",
                    ["amount"] = order.FromAmount,
                    ["totalAmount"] = order.ToAmount,
                    ["fromCurrency"] = fromCurrency?.PersianName ?? "",
                    ["toCurrency"] = toCurrency?.PersianName ?? "",
                    ["fromCurrencyCode"] = fromCurrency?.Code ?? "",
                    ["toCurrencyCode"] = toCurrency?.Code ?? "",
                    ["rate"] = order.Rate,
                    ["oldStatus"] = oldStatus ?? "",
                    ["newStatus"] = newStatus ?? "",
                },
            };
        }

        private async Task<NotificationContext> BuildAccountingDocumentNotificationContextAsync(
            AccountingDocument document,
            NotificationEventType eventType,
            string? userId
        )
        {
            // Use FindAsync for nullable int IDs
            var payerCustomer = document.PayerCustomerId.HasValue
                ? await _context.Customers.FindAsync(document.PayerCustomerId.Value)
                : null;
            var receiverCustomer = document.ReceiverCustomerId.HasValue
                ? await _context.Customers.FindAsync(document.ReceiverCustomerId.Value)
                : null;

            // Use CurrencyId directly - this is why we did the refactoring!
            Currency? currency = null;
            if (document.CurrencyId.HasValue)
            {
                currency = await _context.Currencies.FindAsync(document.CurrencyId.Value);
            }

            var title = eventType switch
            {
                NotificationEventType.AccountingDocumentCreated => "📄 سند حسابداری جدید",
                NotificationEventType.AccountingDocumentVerified => "✅ تأیید سند حسابداری",
                NotificationEventType.AccountingDocumentDeleted => "❌ حذف سند حسابداری",
                _ => "📋 رویداد سند حسابداری",
            };

            var message = eventType switch
            {
                NotificationEventType.AccountingDocumentCreated =>
                    $"{document.Title}: {document.Amount:N0} {currency?.PersianName ?? document.CurrencyCode}",
                NotificationEventType.AccountingDocumentVerified =>
                    $"{document.Title}: {document.Amount:N0} {currency?.PersianName ?? document.CurrencyCode} تأیید شد",
                NotificationEventType.AccountingDocumentDeleted =>
                    $"{document.Title}: {document.Amount:N0} {currency?.PersianName ?? document.CurrencyCode} حذف شد",
                _ => $"رویداد سند #{document.Id}",
            };

            if (payerCustomer != null)
            {
                message += $" از {payerCustomer.FullName}";
            }
            if (receiverCustomer != null)
            {
                message += $" به {receiverCustomer.FullName}";
            }

            var navigationUrl = $"/AccountingDocuments/Details/{document.Id}";
            _logger.LogInformation(
                "Document notification URL generated: {NavigationUrl} for document {DocumentId}",
                navigationUrl,
                document.Id
            );

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority =
                    eventType == NotificationEventType.AccountingDocumentVerified
                        ? NotificationPriority.High
                        : NotificationPriority.Normal,
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "AccountingDocument",
                    EntityId = document.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["payerCustomerId"] = document.PayerCustomerId ?? 0,
                        ["receiverCustomerId"] = document.ReceiverCustomerId ?? 0,
                        ["amount"] = document.Amount,
                        ["currencyId"] = document.CurrencyId ?? 0,
                        ["currencyCode"] = currency != null ? currency.Code : document.CurrencyCode, // Display from navigation
                        ["title"] = document.Title,
                    },
                },
                Data = new Dictionary<string, object>
                {
                    ["documentId"] = document.Id,
                    ["payerCustomerId"] = document.PayerCustomerId ?? 0,
                    ["receiverCustomerId"] = document.ReceiverCustomerId ?? 0,
                    ["payerCustomerName"] = payerCustomer?.FullName ?? "",
                    ["receiverCustomerName"] = receiverCustomer?.FullName ?? "",
                    ["amount"] = document.Amount,
                    ["currencyCode"] = currency?.Code ?? document.CurrencyCode ?? "",
                    ["currencyName"] = currency?.PersianName ?? document.CurrencyCode ?? "",
                    ["title"] = document.Title,
                    ["isVerified"] = document.IsVerified,
                },
                SendToAllAdmins = true, // Always send to all admins
                ExcludeUserIds = !string.IsNullOrEmpty(userId)
                    ? new List<string> { userId }
                    : new List<string>(),
            };
        }

        private Task<NotificationContext> BuildCustomerNotificationContextAsync(
            Customer customer,
            NotificationEventType eventType,
            string? userId
        )
        {
            var title = eventType switch
            {
                NotificationEventType.CustomerRegistered => "👤 مشتری جدید ثبت شد",
                _ => "👤 رویداد مشتری",
            };

            var message = eventType switch
            {
                NotificationEventType.CustomerRegistered =>
                    $"مشتری جدید: {customer.FullName} ({customer.PhoneNumber})",
                _ => $"رویداد مشتری {customer.FullName}",
            };

            var navigationUrl = $"/Customers/Details/{customer.Id}";
            _logger.LogInformation(
                "Customer notification URL generated: {NavigationUrl} for customer {CustomerId}",
                navigationUrl,
                customer.Id
            );

            return Task.FromResult(
                new NotificationContext
                {
                    EventType = eventType,
                    UserId = userId,
                    Title = title,
                    Message = message,
                    NavigationUrl = navigationUrl,
                    Priority = NotificationPriority.Normal,
                    SendToAllAdmins = true, // Always send to all admins
                    ExcludeUserIds = !string.IsNullOrEmpty(userId)
                        ? new List<string> { userId }
                        : new List<string>(),
                    RelatedEntity = new RelatedEntity
                    {
                        EntityType = "Customer",
                        EntityId = customer.Id,
                        EntityData = new Dictionary<string, object>
                        {
                            ["fullName"] = customer.FullName,
                            ["phoneNumber"] = customer.PhoneNumber,
                            ["isActive"] = customer.IsActive,
                        },
                    },
                    Data = new Dictionary<string, object>
                    {
                        ["customerId"] = customer.Id,
                        ["fullName"] = customer.FullName,
                        ["phoneNumber"] = customer.PhoneNumber,
                        ["isActive"] = customer.IsActive,
                    },
                }
            );
        }

        private Task<NotificationContext> BuildManualAdjustmentNotificationContextAsync(
            string title,
            string message,
            NotificationEventType eventType,
            string? userId,
            string? navigationUrl,
            NotificationPriority priority
        )
        {
            // Use explicit URL or default to /admin
            var finalUrl = !string.IsNullOrEmpty(navigationUrl) ? navigationUrl : "/admin";
            _logger.LogInformation(
                "Manual adjustment notification URL: {NavigationUrl} -> {FinalUrl}",
                navigationUrl,
                finalUrl
            );

            return Task.FromResult(
                new NotificationContext
                {
                    EventType = eventType,
                    UserId = userId,
                    Title = title,
                    Message = message,
                    NavigationUrl = finalUrl,
                    Priority = priority,
                    SendToAllAdmins = true, // Always send to all admins
                    ExcludeUserIds = !string.IsNullOrEmpty(userId)
                        ? new List<string> { userId }
                        : new List<string>(),
                    RelatedEntity = new RelatedEntity
                    {
                        EntityType = "ManualAdjustment",
                        EntityId = 0, // No specific entity for manual adjustment notifications
                        EntityData = new Dictionary<string, object>
                        {
                            ["title"] = title,
                            ["message"] = message,
                            ["eventType"] = eventType.ToString(),
                        },
                    },
                    Data = new Dictionary<string, object>
                    {
                        ["title"] = title,
                        ["message"] = message,
                        ["eventType"] = eventType.ToString(),
                        ["priority"] = priority.ToString(),
                        ["navigationUrl"] = finalUrl,
                    },
                }
            );
        }


    }
}
