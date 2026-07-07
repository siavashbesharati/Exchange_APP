using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Admin Activity Log Model - مدل لاگ فعالیت‌های ادمین
    /// Tracks all administrative activities for audit and monitoring
    /// ردیابی تمام فعالیت‌های مدیریتی برای حسابرسی و نظارت
    /// </summary>
    public class AdminActivity
    {
        /// <summary>
        /// Unique identifier for the activity log
        /// شناسه یکتای لاگ فعالیت
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID of the admin user who performed the activity
        /// شناسه کاربر ادمین که فعالیت را انجام داده
        /// </summary>
        [Required]
        [Display(Name = "Admin User ID - شناسه کاربر ادمین")]
        public string AdminUserId { get; set; } = string.Empty;

        /// <summary>
        /// Username of the admin user
        /// نام کاربری ادمین
        /// </summary>
        [Required]
        [StringLength(256)]
        [Display(Name = "Admin Username - نام کاربری ادمین")]
        public string AdminUsername { get; set; } = string.Empty;

        /// <summary>
        /// Type of activity performed
        /// نوع فعالیت انجام شده
        /// </summary>
        [Required]
        [Display(Name = "Activity Type - نوع فعالیت")]
        public AdminActivityType ActivityType { get; set; }

        /// <summary>
        /// Description of the activity
        /// توضیح فعالیت
        /// </summary>
        [Required]
        [StringLength(1000)]
        [Display(Name = "Description - توضیح")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Additional details in JSON format
        /// جزئیات اضافی به فرمت JSON
        /// </summary>
        [StringLength(4000)]
        [Column(TypeName = "TEXT")]
        [Display(Name = "Details - جزئیات")]
        public string? Details { get; set; }

        /// <summary>
        /// IP address of the admin user
        /// آدرس IP کاربر ادمین
        /// </summary>
        [StringLength(45)]
        [Display(Name = "IP Address - آدرس IP")]
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent string from the browser
        /// رشته User Agent از مرورگر
        /// </summary>
        [StringLength(500)]
        [Display(Name = "User Agent - عامل کاربر")]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Timestamp when the activity occurred
        /// زمان وقوع فعالیت
        /// </summary>
        [Required]
        [Display(Name = "Timestamp - زمان")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether the activity was successful
        /// آیا فعالیت موفق بوده
        /// </summary>
        [Display(Name = "Success - موفق")]
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// Entity type that was affected (Order, ExchangeRate, etc.)
        /// نوع موجودیت که تحت تأثیر قرار گرفته
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Entity Type - نوع موجودیت")]
        public string? EntityType { get; set; }

        /// <summary>
        /// ID of the entity that was affected
        /// شناسه موجودیت که تحت تأثیر قرار گرفته
        /// </summary>
        [Display(Name = "Entity ID - شناسه موجودیت")]
        public int? EntityId { get; set; }

        /// <summary>
        /// Old value before the change (for update operations)
        /// مقدار قدیمی قبل از تغییر
        /// </summary>
        [StringLength(2000)]
        [Column(TypeName = "TEXT")]
        [Display(Name = "Old Value - مقدار قدیمی")]
        public string? OldValue { get; set; }

        /// <summary>
        /// New value after the change (for update operations)
        /// مقدار جدید بعد از تغییر
        /// </summary>
        [StringLength(2000)]
        [Column(TypeName = "TEXT")]
        [Display(Name = "New Value - مقدار جدید")]
        public string? NewValue { get; set; }
    }

    /// <summary>
    /// Types of administrative activities
    /// انواع فعالیت‌های مدیریتی
    /// </summary>
    public enum AdminActivityType
    {
        /// <summary>
        /// Order creation
        /// ایجاد معامله
        /// </summary>
        [Display(Name = "Order Created - معامله ایجاد شد")]
        OrderCreated = 1,

        /// <summary>
        /// Order update
        /// بروزرسانی معامله
        /// </summary>
        [Display(Name = "Order Updated - معامله بروزرسانی شد")]
        OrderUpdated = 2,

        /// <summary>
        /// Order cancellation
        /// لغو معامله
        /// </summary>
        [Display(Name = "Order Cancelled - معامله لغو شد")]
        OrderCancelled = 3,

        /// <summary>
        /// Exchange rate creation/update
        /// ایجاد/بروزرسانی نرخ ارز
        /// </summary>
        [Display(Name = "Exchange Rate Updated - نرخ ارز بروزرسانی شد")]
        ExchangeRateUpdated = 4,

        /// <summary>
        /// User management (create, update, delete)
        /// مدیریت کاربران
        /// </summary>
        [Display(Name = "User Management - مدیریت کاربران")]
        UserManagement = 5,

        /// <summary>
        /// User created
        /// کاربر ایجاد شد
        /// </summary>
        [Display(Name = "User Created - کاربر ایجاد شد")]
        UserCreated = 12,

        /// <summary>
        /// User updated
        /// کاربر بروزرسانی شد
        /// </summary>
        [Display(Name = "User Updated - کاربر بروزرسانی شد")]
        UserUpdated = 13,

        /// <summary>
        /// User deleted
        /// کاربر حذف شد
        /// </summary>
        [Display(Name = "User Deleted - کاربر حذف شد")]
        UserDeleted = 14,

        /// <summary>
        /// System settings update
        /// بروزرسانی تنظیمات سیستم
        /// </summary>
        [Display(Name = "Settings Updated - تنظیمات بروزرسانی شد")]
        SettingsUpdated = 6,

        /// <summary>
        /// Login activity
        /// فعالیت ورود به سیستم
        /// </summary>
        [Display(Name = "Login - ورود به سیستم")]
        Login = 7,

        /// <summary>
        /// Logout activity
        /// فعالیت خروج از سیستم
        /// </summary>
        [Display(Name = "Logout - خروج از سیستم")]
        Logout = 8,

        /// <summary>
        /// Failed login attempt
        /// تلاش ناموفق برای ورود
        /// </summary>
        [Display(Name = "Failed Login - ورود ناموفق")]
        FailedLogin = 9,

        /// <summary>
        /// Data export
        /// دریافت داده
        /// </summary>
        [Display(Name = "Data Export - دریافت داده")]
        DataExport = 10,

        /// <summary>
        /// Bulk operations
        /// عملیات انبوه
        /// </summary>
        [Display(Name = "Bulk Operation - عملیات انبوه")]
        BulkOperation = 11,

        /// <summary>
        /// Pool balance change
        /// تغییر موجودی داشبورد
        /// </summary>
        [Display(Name = "Pool Balance Changed - موجودی داشبورد  تغییر یافت")]
        PoolBalanceChanged = 15,

        /// <summary>
        /// Pool statistics reset
        /// ریست آمار داشبورد
        /// </summary>
        [Display(Name = "Pool Stats Reset - ریست آمار داشبورد ")]
        PoolStatsReset = 16,

        /// <summary>
        /// Role permissions updated
        /// دسترسی نقش به سیستم تغییر یافت
        /// </summary>
        [Display(Name = "Role Permissions Updated - دسترسی نقش به سیستم تغییر یافت")]
        RolePermissionsUpdated = 17,

        /// <summary>
        /// Role created
        /// نقش ایجاد شد
        /// </summary>
        [Display(Name = "Role Created - نقش ایجاد شد")]
        RoleCreated = 18,

        /// <summary>
        /// Other administrative activities
        /// سایر فعالیت‌های مدیریتی
        /// </summary>
        [Display(Name = "Other - سایر")]
        Other = 99,
    }
}
