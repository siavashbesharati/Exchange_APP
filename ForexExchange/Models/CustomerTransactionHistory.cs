using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    /// <summary>
    /// Virtual model representing a customer's complete financial transaction history
    /// This is computed dynamically from Orders and AccountingDocuments - no database table needed
    /// </summary>
    public class CustomerTransactionHistory
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public DateTime TransactionDate { get; set; }
        public TransactionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal RunningBalance { get; set; }
        public int? ReferenceId { get; set; } // Order ID or Document ID
        public string? TransactionNumber { get; set; } // Transaction Number from AccountingDocument.ReferenceNumber
        public string? Notes { get; set; }

        // For orders - additional context
        public string? FromCurrency { get; set; }
        public string? ToCurrency { get; set; }
        public decimal? ExchangeRate { get; set; }

        // Navigation
        public Customer Customer { get; set; } = null!;
    }

    public enum TransactionType
    {
        [Display(Name = "موجودی اولیه")]
        InitialBalance = 0,
        
        [Display(Name = "فروش ارز")]
        Sell = 1,
        
        [Display(Name = "خرید ارز")]
        Buy = 2,
        
        [Display(Name = "پرداخت")]
        Document = 3,
        
        [Display(Name = "دریافت")]
        DocumentDebit = 4,
        
        [Display(Name = "تعدیل دستی")]
        ManualAdjustment = 5
    }

    /// <summary>
    /// Service model for customer financial timeline analysis
    /// </summary>
    public class CustomerFinancialTimeline
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<CustomerTransactionHistory> Transactions { get; set; } = new();
        public Dictionary<string, decimal> InitialBalances { get; set; } = new();
        public Dictionary<string, decimal> FinalBalances { get; set; } = new();
        public Dictionary<string, decimal> NetChanges { get; set; } = new();
        
        // Summary statistics
        public int TotalTransactions => Transactions.Count;
        public int OrderCount => Transactions.Count(t => t.Type == TransactionType.Buy || t.Type == TransactionType.Sell);
        public int DocumentCount => Transactions.Count(t => t.Type == TransactionType.Document || t.Type == TransactionType.DocumentDebit);
        public List<string> CurrenciesInvolved => Transactions.Select(t => t.CurrencyCode).Distinct().ToList();
    }

    /// <summary>
    /// Detailed balance snapshot at any point in time
    /// </summary>
    public class CustomerBalanceSnapshot
    {
        public int CustomerId { get; set; }
        public DateTime AsOfDate { get; set; }
        public Dictionary<string, decimal> Balances { get; set; } = new();
        public List<CustomerTransactionHistory> RecentTransactions { get; set; } = new();
    }
}
