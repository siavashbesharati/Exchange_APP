
namespace ForexExchange.Models
{
    /// <summary>
    /// ViewModel for Expenses Report (System Customers and their Bank Accounts)
    /// مدل نمایشی برای گزارش هزینه‌ها (سهامداران و حساب‌های بانکی آنها)
    /// </summary>
    public class ExpensesReportViewModel
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public List<ExpensesReportCurrencyViewModel> Currencies { get; set; } = new();
        public List<ExpensesReportSummaryConversionViewModel> ConvertedSummaries { get; set; } = new();
        public int? SelectedSummaryCurrencyId { get; set; }
        // Display property (backward compatibility)
        public string? SelectedSummaryCurrencyCode { get; set; }

        public decimal TotalBankBalance => Currencies.Sum(c => c.BankTotal);
        public decimal TotalCustomerBalance => Currencies.Sum(c => c.CustomerTotal);
        public decimal TotalDifference => Currencies.Sum(c => c.Difference);

        public bool HasData => Currencies.Any();

        public ExpensesReportSummaryConversionViewModel? DefaultSummary => ConvertedSummaries
            .OrderBy(c => c.RatePriority)
            .ThenBy(c => c.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        public ExpensesReportSummaryConversionViewModel? SelectedSummary
        {
            get
            {
                if (SelectedSummaryCurrencyId.HasValue)
                {
                    return ConvertedSummaries.FirstOrDefault(c => c.CurrencyId == SelectedSummaryCurrencyId.Value);
                }
                // Fallback to CurrencyCode for backward compatibility
                if (!string.IsNullOrWhiteSpace(SelectedSummaryCurrencyCode))
                {
                    return ConvertedSummaries.FirstOrDefault(c =>
                        string.Equals(c.CurrencyCode, SelectedSummaryCurrencyCode, StringComparison.OrdinalIgnoreCase));
                }

                return DefaultSummary;
            }
        }
    }

    public class ExpensesReportCurrencyViewModel
    {
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = string.Empty;
        public decimal BankTotal { get; set; }
        public decimal CustomerTotal { get; set; }
        public decimal Difference => BankTotal + CustomerTotal;
        public List<ExpensesReportBankDetailViewModel> BankDetails { get; set; } = new();
        public List<ExpensesReportCustomerDetailViewModel> CustomerDetails { get; set; } = new();
    }

    public class ExpensesReportBankDetailViewModel
    {
        public int? BankAccountId { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastTransactionAt { get; set; }
    }

    public class ExpensesReportCustomerDetailViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastTransactionAt { get; set; }
        public List<ExpensesReportTransactionViewModel> Transactions { get; set; } = new();
        
        // Summary properties
        public decimal TotalExpenses => Transactions.Where(t => t.TransactionAmount < 0).Sum(t => Math.Abs(t.TransactionAmount));
        public decimal TotalIncome => Transactions.Where(t => t.TransactionAmount > 0).Sum(t => t.TransactionAmount);
        public int TransactionCount => Transactions.Count;
        public decimal NetAmount => TotalIncome - TotalExpenses;
    }

    public class ExpensesReportTransactionViewModel
    {
        public long Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public string? TransactionNumber { get; set; }
    }

    public class ExpensesReportSummaryConversionViewModel
    {
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = string.Empty;
        public int RatePriority { get; set; }
        public decimal BankTotal { get; set; }
        public decimal CustomerTotal { get; set; }
        public decimal Difference => BankTotal + CustomerTotal;
        public bool HasMissingRates { get; set; }
    }
}

