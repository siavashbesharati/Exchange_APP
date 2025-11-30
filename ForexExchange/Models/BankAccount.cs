using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Bank Account Model - مدل حساب بانکی
    /// Manages bank accounts for customers and system
    /// مدیریت حساب‌های بانکی مشتریان و سیستم
    /// </summary>
    public class BankAccount
    {
        /// <summary>
        /// Unique identifier for the bank account
        /// شناسه یکتای حساب بانکی
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Customer ID reference
        /// شناسه مشتری مرجع
        /// </summary>
        [Required]
        [Display(Name = "Customer - مشتری")]
        public int CustomerId { get; set; }

        /// <summary>
        /// Navigation property to Customer
        /// خاصیت ناوبری به مشتری
        /// </summary>
        public Customer Customer { get; set; } = null!;

        /// <summary>
        /// Bank name
        /// نام بانک
        /// </summary>
        [Required]
        [StringLength(100)]
        [Display(Name = "Bank Name - نام بانک")]
        public string BankName { get; set; } = string.Empty;

        /// <summary>
        /// Account number
        /// شماره حساب
        /// </summary>
        [Required]
        [StringLength(50)]
        [Display(Name = "Account Number - شماره حساب")]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Account holder name
        /// نام صاحب حساب
        /// </summary>
        [Required]
        [StringLength(100)]
        [Display(Name = "Account Holder - نام صاحب حساب")]
        public string AccountHolderName { get; set; } = string.Empty;

        /// <summary>
        /// IBAN (International Bank Account Number)
        /// شماره شبا
        /// </summary>
        [StringLength(34)]
        [Display(Name = "IBAN - شماره شبا")]
        public string? IBAN { get; set; }

        /// <summary>
        /// Card number (last 4 digits for security)
        /// شماره کارت (۴ رقم آخر برای امنیت)
        /// </summary>
        [StringLength(4)]
        [Display(Name = "Card Number (Last 4) - شماره کارت (۴ رقم آخر)")]
        public string? CardNumberLast4 { get; set; }

        /// <summary>
        /// Branch name/code
        /// نام/کد شعبه
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Branch - شعبه")]
        public string? Branch { get; set; }

        /// <summary>
        /// Currency of the account
        /// ارز حساب
        /// </summary>
        [Required]
        [StringLength(4)]
        [Display(Name = "Currency - ارز")]
        public string CurrencyCode { get; set; } = "IRR";

        /// <summary>
        /// Currency ID reference
        /// شناسه ارز مرجع
        /// </summary>
        [Display(Name = "Currency ID - شناسه ارز")]
        public int? CurrencyId { get; set; }

        /// <summary>
        /// Is this account active?
        /// آیا این حساب فعال است؟
        /// </summary>
        [Display(Name = "Is Active - فعال")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Is this the default account for the customer?
        /// آیا این حساب پیش‌فرض مشتری است؟
        /// </summary>
        [Display(Name = "Is Default - پیش‌فرض")]
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Account creation date
        /// تاریخ ایجاد حساب
        /// </summary>
        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Last modified date
        /// تاریخ آخرین تغییر
        /// </summary>
        [Display(Name = "Last Modified - آخرین تغییر")]
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Additional notes
        /// یادداشت‌های اضافی
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }

        /// <summary>
        /// Current account balance (in account currency)
        /// موجودی فعلی حساب (بر حسب ارز حساب)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Account Balance - موجودی حساب")]
        public decimal AccountBalance { get; set; } = 0m;

        // Navigation properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        // TODO: Add navigation property for AccountingDocuments in new architecture
        // public ICollection<AccountingDocument> AccountingDocuments { get; set; } = new List<AccountingDocument>();
        
        [Display(Name = "Currency - ارز")]
        public Currency? Currency { get; set; }
    }
}
