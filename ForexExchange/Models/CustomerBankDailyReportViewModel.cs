using System;
using System.Collections.Generic;
using System.Linq;

namespace ForexExchange.Models
{
    public class CustomerBankDailyReportViewModel
    {
        public DateTime ReportDate { get; set; }
        public List<CustomerBankDailyCurrencyViewModel> Currencies { get; set; } = new();
        public List<CustomerBankDailySummaryConversionViewModel> ConvertedSummaries { get; set; } = new();
        public int? SelectedSummaryCurrencyId { get; set; }
        // Display property (backward compatibility)
        public string? SelectedSummaryCurrencyCode { get; set; }

        public decimal TotalBankBalance => Currencies.Sum(c => c.BankTotal);
        public decimal TotalCustomerBalance => Currencies.Sum(c => c.CustomerTotal);
        public decimal TotalDifference => Currencies.Sum(c => c.Difference);

        public bool HasData => Currencies.Any();

        public CustomerBankDailySummaryConversionViewModel? DefaultSummary => ConvertedSummaries
            .OrderBy(c => c.RatePriority)
            .ThenBy(c => c.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        public CustomerBankDailySummaryConversionViewModel? SelectedSummary
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

    public class CustomerBankDailyCurrencyViewModel
    {
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = string.Empty;
        public decimal BankTotal { get; set; }
        public decimal CustomerTotal { get; set; }
        public decimal Difference => BankTotal - CustomerTotal;
        public List<CustomerBankDailyBankDetailViewModel> BankDetails { get; set; } = new();
        public List<CustomerBankDailyCustomerDetailViewModel> CustomerDetails { get; set; } = new();
    }

    public class CustomerBankDailyBankDetailViewModel
    {
        public int? BankAccountId { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastTransactionAt { get; set; }
    }

    public class CustomerBankDailyCustomerDetailViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastTransactionAt { get; set; }
    }

    public class CustomerBankDailySummaryConversionViewModel
    {
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = string.Empty;
        public int RatePriority { get; set; }
        public decimal BankTotal { get; set; }
        public decimal CustomerTotal { get; set; }
        public decimal Difference => BankTotal - CustomerTotal;
        public bool HasMissingRates { get; set; }
    }
}
