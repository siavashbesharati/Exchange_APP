using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public enum DocumentType
    {
        [Display(Name = "نقدی")]
        Cash = 0,
        [Display(Name = " حواله  ")]
        Havala = 1
    }

    public enum PayerType
    {
        Customer = 0,   // مشتری
        System = 1      // سیستم
    }

    public enum ReceiverType
    {
        Customer = 0,   // مشتری
        System = 1      // سیستم
    }

    /// <summary>
    /// Accounting Document (سند حسابداری) - Enhanced for bilateral transactions
    /// Tracks all financial movements between customers, system, and bank accounts
    /// Supports: Customer-to-Customer, Customer-to-System, System-to-Customer, System-to-Bank
    /// </summary>
    public class AccountingDocument : IValidatableObject
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Document Type - نوع سند")]
        public DocumentType Type { get; set; }

        [Required]
        [Display(Name = "Payer Type - نوع پرداخت کننده")]
        public PayerType PayerType { get; set; }

        [Display(Name = "Payer Customer - مشتری پرداخت کننده")]
        public int? PayerCustomerId { get; set; }

        [Display(Name = "Payer Bank Account - حساب بانکی پرداخت کننده")]
        public int? PayerBankAccountId { get; set; }

        [Required]
        [Display(Name = "Receiver Type - نوع دریافت کننده")]
        public ReceiverType ReceiverType { get; set; } = ReceiverType.System;

        [Display(Name = "Receiver Customer - مشتری دریافت کننده")]
        public int? ReceiverCustomerId { get; set; }

        [Display(Name = "Receiver Bank Account - حساب بانکی دریافت کننده")]
        public int? ReceiverBankAccountId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount - مبلغ")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(4)]
        [Display(Name = "Currency - ارز")]
        public string CurrencyCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Document Title - عنوان سند")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description - توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "Document Date - تاریخ سند")]
        public DateTime DocumentDate { get; set; } = DateTime.Now;

        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Is Verified - تأیید شده")]
        public bool IsVerified { get; set; } = false;

        [Display(Name = "Verified At - تاریخ تأیید")]
        public DateTime? VerifiedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Verified By - تأیید شده توسط")]
        public string? VerifiedBy { get; set; }

        // Optional: Reference number for external tracking
        [StringLength(50)]
        [Display(Name = "Reference Number - شماره تراکنش")]
        public string? ReferenceNumber { get; set; }

        // File attachment (optional - for supporting documents)
        [StringLength(100)]
        [Display(Name = "File Name - نام فایل")]
        public string? FileName { get; set; }

        [StringLength(50)]
        [Display(Name = "Content Type - نوع محتوا")]
        public string? ContentType { get; set; }

        [Display(Name = "File Data - داده فایل")]
        public byte[]? FileData { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }

        // Soft Delete Properties
        [Display(Name = "Is Deleted - حذف شده")]
        public bool IsDeleted { get; set; } = false;
        
        [Display(Name = "Deleted At - تاریخ حذف")]
        public DateTime? DeletedAt { get; set; }
        
        [StringLength(100)]
        [Display(Name = "Deleted By - حذف شده توسط")]
        public string? DeletedBy { get; set; }

        // Frozen flag for historical records - excludes from balance calculations
        [Required]
        [Display(Name = "Is Frozen - منجمد شده")]
        public bool IsFrozen { get; set; } = false;

        // PERFORMANCE OPTIMIZATION: Track when this document was last included in a rebuild
        // Used for incremental rebuild to only process changed records
        [Display(Name = "Last Rebuild Timestamp - آخرین زمان بازسازی")]
        public DateTime? LastRebuildTimestamp { get; set; }

        // Navigation properties
        [Display(Name = "Payer Customer - مشتری پرداخت کننده")]
        public Customer? PayerCustomer { get; set; }

        [Display(Name = "Receiver Customer - مشتری دریافت کننده")]
        public Customer? ReceiverCustomer { get; set; }

        [Display(Name = "Payer Bank Account - حساب بانکی پرداخت کننده")]
        public BankAccount? PayerBankAccount { get; set; }

        [Display(Name = "Receiver Bank Account - حساب بانکی دریافت کننده")]
        public BankAccount? ReceiverBankAccount { get; set; }

        // Legacy navigation properties (for backward compatibility)
        [NotMapped]
        [Display(Name = "Customer - مشتری")]
        public Customer? Customer => PayerType == PayerType.Customer ? PayerCustomer : ReceiverCustomer;

        [NotMapped]
        [Display(Name = "Bank Account - حساب بانکی")]
        public BankAccount? BankAccount => PayerType == PayerType.System ? PayerBankAccount : ReceiverBankAccount;

        // Legacy properties for backward compatibility (will be removed after migration)
        [NotMapped]
        [Obsolete("Use PayerCustomerId or ReceiverCustomerId instead")]
        public int CustomerId
        {
            get => PayerType == PayerType.Customer ? (PayerCustomerId ?? 0) : (ReceiverCustomerId ?? 0);
            set
            {
                if (PayerType == PayerType.Customer)
                    PayerCustomerId = value;
                else
                    ReceiverCustomerId = value;
            }
        }

        [NotMapped]
        [Obsolete("Use PayerBankAccountId or ReceiverBankAccountId instead")]
        public int? BankAccountId
        {
            get => PayerType == PayerType.System ? PayerBankAccountId : ReceiverBankAccountId;
            set
            {
                if (PayerType == PayerType.System)
                    PayerBankAccountId = value;
                else
                    ReceiverBankAccountId = value;
            }
        }

        // Helper properties for display
        public string PayerName
        {
            get
            {
                return PayerType switch
                {
                    PayerType.Customer => PayerCustomer?.FullName ?? "مشتری نامشخص",
                    PayerType.System => PayerBankAccount?.BankName + " - " + PayerBankAccount?.AccountNumber ?? "حساب نامشخص",
                    _ => "نامشخص"
                };
            }
        }

        public string ReceiverName
        {
            get
            {
                return ReceiverType switch
                {
                    ReceiverType.Customer => ReceiverCustomer?.FullName ?? "مشتری نامشخص",
                    ReceiverType.System => ReceiverBankAccount?.BankName + " - " + ReceiverBankAccount?.AccountNumber ?? "حساب نامشخص",
                    _ => "نامشخص"
                };
            }
        }

        public string PayerDisplayText
        {
            get
            {
                return PayerType switch
                {
                    PayerType.Customer => $"مشتری: {PayerName}",
                    PayerType.System => $"سیستم: {PayerName}",
                    _ => "نامشخص"
                };
            }
        }

        public string ReceiverDisplayText
        {
            get
            {
                return ReceiverType switch
                {
                    ReceiverType.Customer => $"مشتری: {ReceiverName}",
                    ReceiverType.System => $"سیستم: {ReceiverName}",
                    _ => "نامشخص"
                };
            }
        }

        public string FormattedAmount => $"{Amount:N0} {CurrencyCode}";

        // IValidatableObject implementation for cross-field validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Validate Payer constraints
            if (PayerType == PayerType.Customer && !PayerCustomerId.HasValue)
            {
                results.Add(new ValidationResult(
                    "وقتی نوع پرداخت کننده مشتری است، باید مشتری پرداخت کننده انتخاب شود.",
                    new[] { nameof(PayerCustomerId) }));
            }

            if (PayerType == PayerType.System && !PayerBankAccountId.HasValue)
            {
                results.Add(new ValidationResult(
                    "وقتی نوع پرداخت کننده سیستم است، باید حساب بانکی پرداخت کننده انتخاب شود.",
                    new[] { nameof(PayerBankAccountId) }));
            }

            // Validate Receiver constraints
            if (ReceiverType == ReceiverType.Customer && !ReceiverCustomerId.HasValue)
            {
                results.Add(new ValidationResult(
                    "وقتی نوع دریافت کننده مشتری است، باید مشتری دریافت کننده انتخاب شود.",
                    new[] { nameof(ReceiverCustomerId) }));
            }

            if (ReceiverType == ReceiverType.System && !ReceiverBankAccountId.HasValue)
            {
                results.Add(new ValidationResult(
                    "وقتی نوع دریافت کننده سیستم است، باید حساب بانکی دریافت کننده انتخاب شود.",
                    new[] { nameof(ReceiverBankAccountId) }));
            }

            // Prevent self-transactions for customers
            if (PayerType == PayerType.Customer && ReceiverType == ReceiverType.Customer && 
                PayerCustomerId.HasValue && ReceiverCustomerId.HasValue && 
                PayerCustomerId == ReceiverCustomerId)
            {
                results.Add(new ValidationResult(
                    "مشتری نمی‌تواند به خودش پرداخت کند.",
                    new[] { nameof(PayerCustomerId), nameof(ReceiverCustomerId) }));
            }

            // Prevent self-transactions for bank accounts
            if (PayerType == PayerType.System && ReceiverType == ReceiverType.System && 
                PayerBankAccountId.HasValue && ReceiverBankAccountId.HasValue && 
                PayerBankAccountId == ReceiverBankAccountId)
            {
                results.Add(new ValidationResult(
                    "حساب بانکی نمی‌تواند به خودش انتقال داشته باشد.",
                    new[] { nameof(PayerBankAccountId), nameof(ReceiverBankAccountId) }));
            }

            return results;
        }
    }
}
