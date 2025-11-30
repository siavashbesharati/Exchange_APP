using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Bank Account Balance Model - مدل موجودی حساب بانکی
    /// Used for compatibility with existing service interfaces
    /// برای سازگاری با رابط‌های سرویس موجود استفاده می‌شود
    /// </summary>
    public class BankAccountBalance
    {
        /// <summary>
        /// Unique identifier
        /// شناسه یکتا
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Bank account ID reference
        /// شناسه حساب بانکی مرجع
        /// </summary>
        [Required]
        public int BankAccountId { get; set; }

        /// <summary>
        /// Navigation property to BankAccount
        /// خاصیت ناوبری به حساب بانکی
        /// </summary>
        public BankAccount BankAccount { get; set; } = null!;

        /// <summary>
        /// Navigation property to Currency
        /// خاصیت ناوبری به ارز
        /// </summary>
        public Currency? Currency { get; set; }

        /// <summary>
        /// Currency code
        /// کد ارز
        /// </summary>
        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Currency ID reference
        /// شناسه ارز مرجع
        /// </summary>
        public int? CurrencyId { get; set; }

        /// <summary>
        /// Current balance amount
        /// مبلغ موجودی فعلی
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        /// <summary>
        /// Last updated timestamp
        /// timestamp آخرین بروزرسانی
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Additional notes
        /// یادداشت‌های اضافی
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
