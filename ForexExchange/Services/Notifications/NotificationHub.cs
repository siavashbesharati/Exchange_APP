using ForexExchange.Models;
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
        Task SendOrderNotificationAsync(Order order, NotificationEventType eventType, string? userId = null, string? oldStatus = null, string? newStatus = null);

        /// <summary>
        /// Send notification for accounting document events
        /// ارسال اعلان برای رویدادهای سند حسابداری
        /// </summary>
        Task SendAccountingDocumentNotificationAsync(AccountingDocument document, NotificationEventType eventType, string? userId = null);

        /// <summary>
        /// Send notification for customer events
        /// ارسال اعلان برای رویدادهای مشتری
        /// </summary>
        Task SendCustomerNotificationAsync(Customer customer, NotificationEventType eventType, string? userId = null);

        /// <summary>
        /// Send custom notification
        /// ارسال اعلان معاملهی
        /// </summary>
        Task SendManualAdjustmentNotificationAsync(string title, string message, NotificationEventType eventType = NotificationEventType.ManualAdjustment, string? userId = null, string? navigationUrl = null, NotificationPriority priority = NotificationPriority.Normal);

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

        public NotificationHub(
            ForexDbContext context,
            ILogger<NotificationHub> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
            _providers = new List<INotificationProvider>();
        }

        public void RegisterProvider(INotificationProvider provider)
        {
            if (!_providers.Any(p => p.ProviderName == provider.ProviderName))
            {
                _providers.Add(provider);
                _logger.LogInformation("Registered notification provider: {ProviderName}", provider.ProviderName);
            }
        }

        public IEnumerable<INotificationProvider> GetProviders() => _providers.AsReadOnly();

        /// <summary>
        /// Check if notifications should be disabled in development mode
        /// بررسی اینکه آیا اعلان‌ها در حالت توسعه غیرفعال شوند
        /// </summary>
        private bool ShouldSkipNotification()
        {
            var disableInDevelopment = _configuration.GetValue("Notifications:DisableInDevelopment", false);
            return disableInDevelopment && _environment.IsDevelopment();
        }

        public Task SetProviderEnabledAsync(string providerName, bool enabled)
        {
            // This could be stored in database settings
            var settingKey = $"Notifications:{providerName}:Enabled";
            
            // For now, we'll just log it. In the future, implement database settings
            _logger.LogInformation("Provider {ProviderName} enabled status changed to: {Enabled}", providerName, enabled);
            
            // TODO: Store in SystemSettings table
            // await _settingsService.UpdateSettingAsync(settingKey, enabled.ToString());
            
            return Task.CompletedTask;
        }

        public async Task SendOrderNotificationAsync(Order order, NotificationEventType eventType, string? userId = null, string? oldStatus = null, string? newStatus = null)
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug("Skipping order notification in development mode for order {OrderId}, event {EventType}", order.Id, eventType);
                return;
            }

            try
            {
                var context = await BuildOrderNotificationContextAsync(order, eventType, userId, oldStatus, newStatus);
                await SendNotificationToProvidersAsync(context, provider => provider.SendOrderNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending order notification for order {OrderId}, event {EventType}", order.Id, eventType);
            }
        }

        public async Task SendAccountingDocumentNotificationAsync(AccountingDocument document, NotificationEventType eventType, string? userId = null)
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug("Skipping document notification in development mode for document {DocumentId}, event {EventType}", document.Id, eventType);
                return;
            }

            try
            {
                var context = await BuildAccountingDocumentNotificationContextAsync(document, eventType, userId);
                await SendNotificationToProvidersAsync(context, provider => provider.SendAccountingDocumentNotificationAsync(context));
            }
            catch (Exception ex)
            {
                // Idempotent: Log error but don't throw - notification failures should not affect main operations
                _logger.LogError(ex, "Error sending accounting document notification for document {DocumentId}, event {EventType}. Notification failed but document operation succeeded. ExceptionType: {ExceptionType}", 
                    document.Id, eventType, ex.GetType().Name);
                
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerExceptionType} - {InnerExceptionMessage}", 
                        ex.InnerException.GetType().Name, ex.InnerException.Message);
                }
                
                // Don't throw - this is idempotent
            }
        }

        public async Task SendCustomerNotificationAsync(Customer customer, NotificationEventType eventType, string? userId = null)
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug("Skipping customer notification in development mode for customer {CustomerId}, event {EventType}", customer.Id, eventType);
                return;
            }

            try
            {
                var context = await BuildCustomerNotificationContextAsync(customer, eventType, userId);
                await SendNotificationToProvidersAsync(context, provider => provider.SendCustomerNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending customer notification for customer {CustomerId}, event {EventType}", customer.Id, eventType);
            }
        }

        public async Task SendManualAdjustmentNotificationAsync(string title, string message, NotificationEventType eventType = NotificationEventType.ManualAdjustment, string? userId = null, string? navigationUrl = null, NotificationPriority priority = NotificationPriority.Normal)
        {
            if (ShouldSkipNotification())
            {
                _logger.LogDebug("Skipping custom notification in development mode: {Title}", title);
                return;
            }

            try
            {
                var context = await BuildManualAdjustmentNotificationContextAsync(title, message, eventType, userId, navigationUrl, priority);
                await SendNotificationToProvidersAsync(context, provider => provider.SendManualAdjustmentNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending custom notification: {Title}", title);
            }
        }

        private async Task SendNotificationToProvidersAsync(NotificationContext context, Func<INotificationProvider, Task> sendAction)
        {
            var enabledProviders = _providers.Where(p => p.IsEnabled).ToList();
            
            if (!enabledProviders.Any())
            {
                _logger.LogWarning("No enabled notification providers found for event {EventType}", context.EventType);
                return;
            }

            var tasks = enabledProviders.Select(async provider =>
            {
                try
                {
                    await sendAction(provider);
                    _logger.LogDebug("Notification sent via {ProviderName} for event {EventType}", provider.ProviderName, context.EventType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending notification via {ProviderName} for event {EventType}", provider.ProviderName, context.EventType);
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task<NotificationContext> BuildOrderNotificationContextAsync(Order order, NotificationEventType eventType, string? userId, string? oldStatus, string? newStatus)
        {
            var customer = await _context.Customers.FindAsync(order.CustomerId);
            var fromCurrency = await _context.Currencies.FindAsync(order.FromCurrencyId);
            var toCurrency = await _context.Currencies.FindAsync(order.ToCurrencyId);

            var title = eventType switch
            {
                NotificationEventType.OrderCreated => "🔔 معامله جدید ثبت شد",
                NotificationEventType.OrderDeleted => "❌ معامله حذف شد",
                _ => "📋 رویداد معامله"
            };

            var message = eventType switch
            {
                NotificationEventType.OrderCreated => $"معامله #{order.Id} برای {customer?.FullName ?? "نامعلوم"}: {order.FromAmount:N0} {fromCurrency?.PersianName} → {order.ToAmount:N0} {toCurrency?.PersianName}",
                NotificationEventType.OrderDeleted => $"معامله #{order.Id} برای {customer?.FullName ?? "نامعلوم"} لغو شد",
                _ => $"رویداد معامله #{order.Id}"
            };

            var navigationUrl = $"/Orders/Details/{order.Id}";
            _logger.LogInformation("Order notification URL generated: {NavigationUrl} for order {OrderId}", navigationUrl, order.Id);

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = NotificationPriority.Normal,
                SendToAllAdmins = true, // Always send to all admins
                ExcludeUserIds = !string.IsNullOrEmpty(userId) ? new List<string> { userId } : new List<string>(),
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
                        ["rate"] = order.Rate
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["orderId"] = order.Id,
                    ["customerId"] = order.CustomerId,
                    ["amount"] = order.FromAmount,
                    ["totalAmount"] = order.ToAmount,
                    ["fromCurrency"] = fromCurrency?.PersianName ?? "",
                    ["toCurrency"] = toCurrency?.PersianName ?? "",
                    ["oldStatus"] = oldStatus ?? "",
                    ["newStatus"] = newStatus ?? ""
                }
            };
        }

        private async Task<NotificationContext> BuildAccountingDocumentNotificationContextAsync(AccountingDocument document, NotificationEventType eventType, string? userId)
        {
            // Use FindAsync for nullable int IDs
            var payerCustomer = document.PayerCustomerId.HasValue 
                ? await _context.Customers.FindAsync(document.PayerCustomerId.Value) 
                : null;
            var receiverCustomer = document.ReceiverCustomerId.HasValue 
                ? await _context.Customers.FindAsync(document.ReceiverCustomerId.Value) 
                : null;
            
            // Use FirstOrDefaultAsync for string CurrencyCode (not int Id)
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == document.CurrencyCode);

            var title = eventType switch
            {
                NotificationEventType.AccountingDocumentCreated => "📄 سند حسابداری جدید",
                NotificationEventType.AccountingDocumentVerified => "✅ تأیید سند حسابداری",
                NotificationEventType.AccountingDocumentDeleted => "❌ حذف سند حسابداری",
                _ => "📋 رویداد سند حسابداری"
            };

            var message = eventType switch
            {
                NotificationEventType.AccountingDocumentCreated => $"{document.Title}: {document.Amount:N0} {currency?.PersianName ?? document.CurrencyCode}",
                NotificationEventType.AccountingDocumentVerified => $"{document.Title}: {document.Amount:N0} {currency?.PersianName ?? document.CurrencyCode} تأیید شد",
                NotificationEventType.AccountingDocumentDeleted => $"{document.Title}: {document.Amount:N0} {currency?.PersianName ?? document.CurrencyCode} حذف شد",
                _ => $"رویداد سند #{document.Id}"
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
            _logger.LogInformation("Document notification URL generated: {NavigationUrl} for document {DocumentId}", navigationUrl, document.Id);

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = eventType == NotificationEventType.AccountingDocumentVerified ? NotificationPriority.High : NotificationPriority.Normal,
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "AccountingDocument",
                    EntityId = document.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["payerCustomerId"] = document.PayerCustomerId ?? 0,
                        ["receiverCustomerId"] = document.ReceiverCustomerId ?? 0,
                        ["amount"] = document.Amount,
                        ["currencyCode"] = document.CurrencyCode,
                        ["title"] = document.Title
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["documentId"] = document.Id,
                    ["payerCustomerId"] = document.PayerCustomerId ?? 0,
                    ["receiverCustomerId"] = document.ReceiverCustomerId ?? 0,
                    ["amount"] = document.Amount,
                    ["currencyCode"] = document.CurrencyCode,
                    ["title"] = document.Title,
                    ["isVerified"] = document.IsVerified
                },
                SendToAllAdmins = true, // Always send to all admins
                ExcludeUserIds = !string.IsNullOrEmpty(userId) ? new List<string> { userId } : new List<string>()
            };
        }

        private Task<NotificationContext> BuildCustomerNotificationContextAsync(Customer customer, NotificationEventType eventType, string? userId)
        {
            var title = eventType switch
            {
                NotificationEventType.CustomerRegistered => "👤 مشتری جدید ثبت شد",
                _ => "👤 رویداد مشتری"
            };

            var message = eventType switch
            {
                NotificationEventType.CustomerRegistered => $"مشتری جدید: {customer.FullName} ({customer.PhoneNumber})",
                _ => $"رویداد مشتری {customer.FullName}"
            };

            var navigationUrl = $"/Customers/Details/{customer.Id}";
            _logger.LogInformation("Customer notification URL generated: {NavigationUrl} for customer {CustomerId}", navigationUrl, customer.Id);

            return Task.FromResult(new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = NotificationPriority.Normal,
                SendToAllAdmins = true, // Always send to all admins
                ExcludeUserIds = !string.IsNullOrEmpty(userId) ? new List<string> { userId } : new List<string>(),
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "Customer",
                    EntityId = customer.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["fullName"] = customer.FullName,
                        ["phoneNumber"] = customer.PhoneNumber,
                        ["isActive"] = customer.IsActive
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["customerId"] = customer.Id,
                    ["fullName"] = customer.FullName,
                    ["phoneNumber"] = customer.PhoneNumber,
                    ["isActive"] = customer.IsActive
                }
            });
        }

        private Task<NotificationContext> BuildManualAdjustmentNotificationContextAsync(string title, string message, NotificationEventType eventType, string? userId, string? navigationUrl, NotificationPriority priority)
        {
            // Use explicit URL or default to /admin
            var finalUrl = !string.IsNullOrEmpty(navigationUrl) ? navigationUrl : "/admin";
            _logger.LogInformation("Manual adjustment notification URL: {NavigationUrl} -> {FinalUrl}", navigationUrl, finalUrl);

            return Task.FromResult(new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = finalUrl,
                Priority = priority,
                SendToAllAdmins = true, // Always send to all admins
                ExcludeUserIds = !string.IsNullOrEmpty(userId) ? new List<string> { userId } : new List<string>(),
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "ManualAdjustment",
                    EntityId = 0, // No specific entity for manual adjustment notifications
                    EntityData = new Dictionary<string, object>
                    {
                        ["title"] = title,
                        ["message"] = message,
                        ["eventType"] = eventType.ToString()
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["title"] = title,
                    ["message"] = message,
                    ["eventType"] = eventType.ToString(),
                    ["priority"] = priority.ToString(),
                    ["navigationUrl"] = finalUrl
                }
            });
        }
    }
}
