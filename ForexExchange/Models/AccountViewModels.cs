using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل (اختیاری)")]
        public string? Email { get; set; }

        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور")]
        [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن باید یکسان باشند")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "کد ملی")]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }
    }

    public class LoginViewModel
    {
        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد یکبارمصرف الزامی است")]
        [Display(Name = "کد یکبارمصرف")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "کد یکبارمصرف باید 6 رقم باشد")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "کد یکبارمصرف باید فقط شامل 6 عدد باشد")]
        public string OtpCode { get; set; } = string.Empty;

        [Display(Name = "مرا به خاطر بسپار")]
        public bool RememberMe { get; set; }
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "کد ملی")]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [Display(Name = "نقش کاربری")]
        public string Role { get; set; } = string.Empty;
    }

    public class CustomerCreateViewModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل (اختیاری)")]

        public string? Email { get; set; }

        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(20)]
        public string SecondaryPhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        [StringLength(100, ErrorMessage = "رمز عبور باید حداقل {2} کاراکتر باشد", MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور")]
        [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن باید یکسان باشند")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "کد ملی")]
        [StringLength(10, ErrorMessage = "کد ملی باید 10 رقم باشد", MinimumLength = 10)]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "جنسیت")]
        public bool Gender { get; set; } = true;

        [Display(Name = "سهامدار")]
        public bool IsShareHolder { get; set; } = false;

        // Initial balances per currency (code -> amount). Allow negative and positive.
        public Dictionary<string, decimal> InitialBalances { get; set; } = new();
    }

    public class CustomerEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل (اختیاری)")]
        public string? Email { get; set; }

        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(20)]
        public string SecondaryPhoneNumber { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور جدید (اختیاری)")]
        [StringLength(100, ErrorMessage = "رمز عبور باید حداقل {2} کاراکتر باشد", MinimumLength = 6)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور جدید")]
        [Compare("NewPassword", ErrorMessage = "رمز عبور و تکرار آن باید یکسان باشند")]
        public string? ConfirmNewPassword { get; set; }

        [Display(Name = "کد ملی")]
        [StringLength(10, ErrorMessage = "کد ملی باید 10 رقم باشد", MinimumLength = 10)]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        public bool Gender { get; set; } = true;

        [Display(Name = "مشتری سیستمی")]
        public bool IsSystem { get; set; } = false;

        [Display(Name = "سهامدار")]
        public bool IsShareHolder { get; set; } = false;

        public DateTime CreatedAt { get; set; }

        // Initial balances per currency (code -> amount). Allow negative and positive.
        public Dictionary<string, decimal> InitialBalances { get; set; } = new();
    }

    public class DatabaseManagementViewModel
    {
        public int CustomersCount { get; set; }
        public int OrdersCount { get; set; }
        public int CurrencyPoolsCount { get; set; }
        public int TransactionsCount { get; set; }
        public int ExchangeRatesCount { get; set; }
        public int AccountingDocumentsCount { get; set; }
    }

    /// <summary>
    /// Generic financial report view model for printing reports
    /// مدل نمای کلی گزارش مالی برای چاپ گزارش‌ها
    /// </summary>
    public class FinancialReportViewModel
    {
        public string ReportType { get; set; } = string.Empty; // "Customer", "BankAccount", "Pool"
        public string EntityName { get; set; } = string.Empty; // Customer name, Bank account name, Currency code
        public int? EntityId { get; set; } // Customer ID, Bank Account ID, or null for Pool
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<FinancialTransactionItem> Transactions { get; set; } = new();
        public Dictionary<string, decimal> FinalBalances { get; set; } = new();
        public Dictionary<string, decimal> InitialBalances { get; set; } = new();
        public string ReportTitle { get; set; } = string.Empty;
        public string ReportSubtitle { get; set; } = string.Empty;
    }

    /// <summary>
    /// Generic financial transaction item for reports
    /// آیتم تراکنش مالی کلی برای گزارش‌ها
    /// </summary>
    public class FinancialTransactionItem
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal RunningBalance { get; set; }
        public int? ReferenceId { get; set; }
        public bool CanNavigate { get; set; }
        public string? TransactionNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty; // Customer name for Order transactions

        // For orders - additional context
        public string? FromCurrency { get; set; }
        public string? ToCurrency { get; set; }
        public decimal? ExchangeRate { get; set; }

        // Pool-specific other side currency fields
        public string? PairedCurrencyCode { get; set; }
        public decimal PairedAmount { get; set; }
    }
}
