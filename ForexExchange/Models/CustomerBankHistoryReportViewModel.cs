

namespace ForexExchange.Models
{
    /// <summary>
    /// ViewModel for Customer-Bank History Report (date range)
    /// مدل نمایشی برای گزارش تاریخچه مشتریان و بانک‌ها (بازه زمانی)
    /// </summary>
    public class CustomerBankHistoryReportViewModel
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public List<CustomerBankHistoryCurrencyViewModel> Currencies { get; set; } = new();
        public List<CustomerBankHistorySummaryConversionViewModel> ConvertedSummaries { get; set; } = new();
        public int? SelectedSummaryCurrencyId { get; set; }
        // Display property (backward compatibility)
        public string? SelectedSummaryCurrencyCode { get; set; }

        public decimal TotalBankBalance => Currencies.Sum(c => c.BankTotal);
        public decimal TotalCustomerBalance => Currencies.Sum(c => c.CustomerTotal);
        public decimal TotalDifference => Currencies.Sum(c => c.Difference);

        public bool HasData => Currencies.Any();

        public CustomerBankHistorySummaryConversionViewModel? DefaultSummary => ConvertedSummaries
            .OrderBy(c => c.RatePriority)
            .ThenBy(c => c.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        public CustomerBankHistorySummaryConversionViewModel? SelectedSummary
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

    public class CustomerBankHistoryCurrencyViewModel
    {
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = string.Empty;
        public decimal BankTotal { get; set; }
        public decimal CustomerTotal { get; set; }
        public decimal ShareholderTotal { get; set; } // موجودی سهامداران
        public decimal Difference => BankTotal + CustomerTotal;
        public List<CustomerBankHistoryBankDetailViewModel> BankDetails { get; set; } = new();
        public List<CustomerBankHistoryCustomerDetailViewModel> CustomerDetails { get; set; } = new();
        public List<CustomerBankHistoryCustomerDetailViewModel> ShareholderDetails { get; set; } = new(); // جزئیات سهامداران
    }

    public class CustomerBankHistoryBankDetailViewModel
    {
        public int? BankAccountId { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastTransactionAt { get; set; }
    }

    public class CustomerBankHistoryCustomerDetailViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastTransactionAt { get; set; }
    }

    public class CustomerBankHistorySummaryConversionViewModel
    {
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = string.Empty;
        public int RatePriority { get; set; }
        public decimal BankTotal { get; set; }
        public decimal CustomerTotal { get; set; }
        public decimal ShareholderTotal { get; set; } // موجودی سهامداران
        public decimal Difference => BankTotal + CustomerTotal;
        public bool HasMissingRates { get; set; }
    }
}
