using System.Collections.Generic;

namespace ForexExchange.Models
{
    public class AllCustomerBalancePrintViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<BalanceItem> Balances { get; set; } = new();

        public class BalanceItem
        {
            public int CurrencyId { get; set; }
            // Display property (from Currency navigation)
            public string CurrencyCode { get; set; } = string.Empty;
            public decimal Balance { get; set; }
        }
    }

    public class AllCustomersBalanceCurrencyTotal
    {
        public decimal TotalCredit { get; set; }
        public decimal TotalDebt { get; set; }
        public decimal NetBalance { get; set; }
        public int CustomerCount { get; set; }
    }

    public class AllCustomersBalanceSummary
    {
        public int TotalCustomersWithBalances { get; set; }
        public int TotalCustomersWithCredit { get; set; }
        public int TotalCustomersWithDebt { get; set; }
        public string? CurrencyFilter { get; set; }
        public Dictionary<string, AllCustomersBalanceCurrencyTotal> CurrencyTotals { get; set; } = new();
    }

    public class AllCustomersBalanceReportData
    {
        public List<AllCustomerBalancePrintViewModel> Customers { get; set; } = new();
        public AllCustomersBalanceSummary Summary { get; set; } = new();
    }
}
