using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Customer Balance History Transaction Types
    /// انواع تراکنش‌های تاریخچه موجودی مشتری
    /// </summary>
    public enum CustomerBalanceTransactionType
    {
        [Display(Name = "Order - معامله")]
        Order = 1,
        
        [Display(Name = "Accounting Document - سند حسابداری")]
        AccountingDocument = 2,
        
        [Display(Name = "Manual - دستی")]
        Manual = 3
    }

    /// <summary>
    /// Customer Balance History - Event Sourcing for Customer Balances
    /// تاریخچه موجودی مشتری - منبع رویدادها برای موجودی مشتریان
    /// 
    /// CRITICAL: This maintains EXACT same calculation logic as existing CustomerBalance system
    /// </summary>
    [Table("CustomerBalanceHistory")]
    public class CustomerBalanceHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Display(Name = "Customer ID - شناسه مشتری")]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(3)]
        [Display(Name = "Currency Code - کد ارز")]
        public string CurrencyCode { get; set; } = string.Empty;

        [Display(Name = "Currency ID - شناسه ارز")]
        public int? CurrencyId { get; set; }

        [Required]
        [Display(Name = "Transaction Type - نوع تراکنش")]
        public CustomerBalanceTransactionType TransactionType { get; set; }

        [Display(Name = "Reference ID - شناسه مرجع")]
        public int? ReferenceId { get; set; } // OrderId or DocumentId (null for Manual transactions)

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Balance Before - موجودی قبل")]
        public decimal BalanceBefore { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Transaction Amount - مقدار تراکنش")]
        public decimal TransactionAmount { get; set; } // +/- amount

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Balance After - موجودی بعد")]
        public decimal BalanceAfter { get; set; }

        [StringLength(500)]
        [Display(Name = "Description - توضیحات")]
        public string? Description { get; set; }

        [StringLength(500)]
        [Display(Name = "Note - یادداشت")]
        public string? Note { get; set; }

        [StringLength(50)]
        [Display(Name = "Transaction Number - شماره تراکنش")]
        public string? TransactionNumber { get; set; }

        [Required]
        [Display(Name = "Transaction Date - تاریخ تراکنش")]
        public DateTime TransactionDate { get; set; }

        [Required]
        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        [Display(Name = "Created By - ایجاد شده توسط")]
        public string? CreatedBy { get; set; }

        // NEW: Soft delete flags
        [Required]
        [Display(Name = "Is Deleted - حذف شده")]
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted At - تاریخ حذف")]
        public DateTime? DeletedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Deleted By - حذف شده توسط")]
        public string? DeletedBy { get; set; }

        // Navigation properties
        public virtual Customer Customer { get; set; } = null!;

        public virtual Currency? Currency { get; set; }

        /// <summary>
        /// Validates that BalanceAfter = BalanceBefore + TransactionAmount
        /// اعتبارسنجی که موجودی بعد = موجودی قبل + مقدار تراکنش
        /// </summary>
        public bool IsCalculationValid()
        {
            return Math.Abs((BalanceBefore + TransactionAmount) - BalanceAfter) < 0.0001m;
        }

        /// <summary>
        /// Auto-calculate BalanceAfter from BalanceBefore + TransactionAmount
        /// محاسبه خودکار موجودی بعد از موجودی قبل + مقدار تراکنش
        /// </summary>
        public void CalculateBalanceAfter()
        {
            BalanceAfter = BalanceBefore + TransactionAmount;
        }
    }
}
