using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Customer Balance (موجودی مشتری) - replaces CustomerInitialBalance
    /// Tracks current balance for each customer in each currency
    /// Updated by: Orders, Accounting Documents, Manual Adjustments
    /// </summary>
    public class CustomerBalance
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Customer - مشتری")]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(3)]
        [Display(Name = "Currency Code - کد ارز")]
        public string CurrencyCode { get; set; } = string.Empty;

        [Display(Name = "Currency ID - شناسه ارز")]
        public int? CurrencyId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Balance - موجودی")]
        public decimal Balance { get; set; }

        [Display(Name = "Last Updated - آخرین بروزرسانی")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }

        // Navigation properties
        [Display(Name = "Customer - مشتری")]
        public Customer Customer { get; set; } = null!;

        [Display(Name = "Currency - ارز")]
        public Currency? Currency { get; set; }

        // Helper properties
        public bool IsDebt => Balance < 0;
        public bool IsCredit => Balance > 0;
        public decimal AbsoluteBalance => Math.Abs(Balance);
        public string FormattedBalance => $"{Balance:N0} {CurrencyCode}";
        public string BalanceStatus => Balance switch
        {
            > 0 => "اعتبار",
            < 0 => "بدهی", 
            _ => "تسویه"
        };
    }

    /// <summary>
    /// Summary view model for customer debt/credit across all currencies
    /// </summary>
    public class CustomerBalanceSummary
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public List<CustomerBalance> CurrencyBalances { get; set; } = new();
        public decimal NetBalanceInPrimaryCurrency { get; set; }
        public string PrimaryCurrency { get; set; } = "IRR";
        public int ActiveBalanceCount => CurrencyBalances.Count(b => b.Balance != 0);
        public bool HasDebt => CurrencyBalances.Any(b => b.Balance < 0);
        public bool HasCredit => CurrencyBalances.Any(b => b.Balance > 0);
    }
}
