using ForexExchange.Models;

namespace ForexExchange.Services.Notifications
{
    /// <summary>
    /// Base interface for all notification providers
    /// رابط پایه برای تمام ارائه‌دهندگان اعلان
    /// </summary>
    public interface INotificationProvider
    {
        /// <summary>
        /// Name of the notification provider (SignalR, Push, SMS, Email, etc.)
        /// نام ارائه‌دهنده اعلان
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Whether this provider is enabled
        /// آیا این ارائه‌دهنده فعال است
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Send notification for order events
        /// ارسال اعلان برای رویدادهای معامله
        /// </summary>
        Task SendOrderNotificationAsync(NotificationContext context);

        /// <summary>
        /// Send notification for accounting document events
        /// ارسال اعلان برای رویدادهای سند حسابداری
        /// </summary>
        Task SendAccountingDocumentNotificationAsync(NotificationContext context);

        /// <summary>
        /// Send notification for customer events
        /// ارسال اعلان برای رویدادهای مشتری
        /// </summary>
        Task SendCustomerNotificationAsync(NotificationContext context);

        /// <summary>
        /// Send notification for system events
        /// ارسال اعلان برای رویدادهای سیستم
        /// </summary>
        Task SendSystemNotificationAsync(NotificationContext context);

        /// <summary>
        /// Send custom notification
        /// ارسال اعلان معاملهی
        /// </summary>
        Task SendManualAdjustmentNotificationAsync(NotificationContext context);
    }

    /// <summary>
    /// Notification context containing all relevant information
    /// زمینه اعلان شامل تمام اطلاعات مربوطه
    /// </summary>
    public class NotificationContext
    {
        /// <summary>
        /// Type of event that triggered the notification
        /// نوع رویدادی که اعلان را تریگر کرده
        /// </summary>
        public NotificationEventType EventType { get; set; }

        /// <summary>
        /// User ID who triggered the event
        /// شناسه کاربری که رویداد را تریگر کرده
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Target user IDs to receive notification (if specific users)
        /// شناسه‌های کاربران هدف برای دریافت اعلان
        /// </summary>
        public List<string> TargetUserIds { get; set; } = new();

        /// <summary>
        /// User IDs to exclude from notifications (e.g., the user who triggered the event)
        /// شناسه‌های کاربرانی که از دریافت اعلان مستثنی هستند
        /// </summary>
        public List<string> ExcludeUserIds { get; set; } = new();

        /// <summary>
        /// Whether to send to all admin users
        /// آیا به همه کاربران ادمین ارسال شود
        /// </summary>
        public bool SendToAllAdmins { get; set; } = true;

        /// <summary>
        /// Notification title
        /// عنوان اعلان
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Notification message
        /// متن اعلان
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional data specific to the event
        /// داده‌های اضافی مختص رویداد
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Priority level of the notification
        /// سطح اولویت اعلان
        /// </summary>
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        /// <summary>
        /// URL for navigation (for clickable notifications)
        /// آدرس برای ناوبری (برای اعلان‌های قابل کلیک)
        /// </summary>
        public string? NavigationUrl { get; set; }

        /// <summary>
        /// Related entity information
        /// اطلاعات موجودیت مرتبط
        /// </summary>
        public RelatedEntity? RelatedEntity { get; set; }

        /// <summary>
        /// Information about the user who triggered the event
        /// اطلاعات کاربر انجام‌دهنده رویداد
        /// </summary>
        public NotificationActorInfo Actor { get; set; } = new();

        /// <summary>
        /// When the event occurred
        /// زمان وقوع رویداد
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Actor metadata for audit-style notifications
    /// </summary>
    public class NotificationActorInfo
    {
        public string UserName { get; set; } = "نامشخص";
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string Browser { get; set; } = "نامشخص";
        public string OperatingSystem { get; set; } = "نامشخص";
    }

    /// <summary>
    /// Types of notification events
    /// انواع رویدادهای اعلان
    /// </summary>
    public enum NotificationEventType
    {
        // Order events
        OrderCreated,
        OrderDeleted,

        // Accounting document events
        AccountingDocumentCreated,
        AccountingDocumentVerified,

        AccountingDocumentDeleted,

        // Customer events
        CustomerRegistered,

        // Task events
        TaskAssignment,
        TaskDueReminder,
        TaskOverdue,
        TaskProgress,
        TaskCompleted,

        // System events
        SystemError,
        SystemMaintenance,

        // Custom events
        ManualAdjustment,

        PublicEvent
    }

    /// <summary>
    /// Notification priority levels
    /// سطوح اولویت اعلان
    /// </summary>
    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Information about related entity
    /// اطلاعات موجودیت مرتبط
    /// </summary>
    public class RelatedEntity
    {
        public string EntityType { get; set; } = string.Empty; // "Order", "AccountingDocument", "Customer", etc.
        public int EntityId { get; set; }
        public Dictionary<string, object> EntityData { get; set; } = new();
    }
}
