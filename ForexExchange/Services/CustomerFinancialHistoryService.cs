using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services.Interfaces;

namespace ForexExchange.Services
{
    /// <summary>
    /// Customer Financial History Service
    /// Reconstructs complete transaction timeline from existing Orders and AccountingDocuments
    /// NO DATABASE CHANGES NEEDED - Pure computation from existing data
    /// </summary>
    public class CustomerFinancialHistoryService : BaseFinancialService
    {
        private readonly ILogger<CustomerFinancialHistoryService> _logger;

        public CustomerFinancialHistoryService(ForexDbContext context, ILogger<CustomerFinancialHistoryService> logger)
            : base(context)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get complete financial timeline for a customer
        /// This reconstructs the entire history from Orders and AccountingDocuments
        /// FIXED: Properly calculates initial balances when date filtering is applied
        /// </summary>
        public async Task<CustomerFinancialTimeline> GetCustomerTimelineAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, string? currencyCode = null)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                    throw new ArgumentException($"Customer with ID {customerId} not found");

                // Use base class helper for date validation
                var (validFromDate, validToDate) = ValidateDateRange(fromDate, toDate);
                var isDateFiltered = fromDate.HasValue || toDate.HasValue;

                _logger.LogInformation($"Getting timeline for customer {customerId} from {validFromDate} to {validToDate}, DateFiltered: {isDateFiltered}");

                var timeline = new CustomerFinancialTimeline
                {
                    // Name uses gender as prefix
                    CustomerId = 100 + customerId,
                    CustomerName = (customer.Gender ? "Mr. " : "Ms. ") + customer.FullName,
                    FromDate = validFromDate,
                    ToDate = validToDate
                };

                // NEW APPROACH: Use CustomerBalanceHistory table directly - EXCLUDE ONLY DELETED RECORDS FOR REPORTING
                var historyQuery = _context.CustomerBalanceHistory
                    .Where(h => h.CustomerId == customerId &&
                               h.TransactionDate >= validFromDate &&
                               h.TransactionDate <= validToDate &&
                               !h.IsDeleted); // EXCLUDE ONLY DELETED RECORDS FOR REPORTING

                // Apply currency filter if specified
                if (!string.IsNullOrEmpty(currencyCode))
                {
                    historyQuery = historyQuery.Where(h => h.CurrencyCode == currencyCode);
                }

                var historyRecords = await historyQuery
                    .AsNoTracking()
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                _logger.LogInformation($"Found {historyRecords.Count} balance history records for customer {customerId}");

                // Convert history records to transaction history format
                var allTransactions = historyRecords.Select(h => new CustomerTransactionHistory
                {
                    Id = (int)h.Id, // Set the CustomerBalanceHistory ID for deletion functionality (cast long to int)
                    CustomerId = h.CustomerId,
                    TransactionDate = h.TransactionDate,
                    Type = GetTransactionTypeFromEnum(h.TransactionType, h.TransactionAmount),
                    Description = h.Description ?? GetDefaultDescription(h.TransactionType, h.ReferenceId),
                    CurrencyCode = h.CurrencyCode,
                    Amount = h.TransactionAmount,
                    RunningBalance = h.BalanceAfter, // Use BalanceAfter as running balance
                    ReferenceId = h.ReferenceId, // Can be null for Manual transactions
                    TransactionNumber = h.TransactionNumber, // Add TransactionNumber from balance history
                    Notes = h.Note,
                }).ToList();

                // Calculate initial balances from first record per currency
                var initialBalances = new Dictionary<string, decimal>();
                var currencyGroups = historyRecords.GroupBy(h => h.CurrencyCode);

                foreach (var group in currencyGroups)
                {
                    var firstRecord = group.First(); // First history record for this currency

                    // ALWAYS use BalanceBefore from first record as initial balance
                    initialBalances[firstRecord.CurrencyCode] = firstRecord.BalanceBefore;
                }

                timeline.InitialBalances = initialBalances;
                timeline.Transactions = allTransactions;

                // Final balances are the last BalanceAfter for each currency
                timeline.FinalBalances = currencyGroups.ToDictionary(
                    g => g.Key,
                    g => g.Last().BalanceAfter // Use BalanceAfter from last history record
                );

                // Calculate net changes
                timeline.NetChanges = timeline.FinalBalances.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value - (timeline.InitialBalances.ContainsKey(kvp.Key) ? timeline.InitialBalances[kvp.Key] : 0)
                );

                return timeline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer financial timeline for customer {CustomerId}", customerId);
                throw;
            }
        }

        /// <summary>
        /// Helper method to convert enum transaction type to CustomerTransactionHistory enum
        /// Considers both transaction type and amount sign for proper classification
        /// </summary>
        private TransactionType GetTransactionTypeFromEnum(CustomerBalanceTransactionType transactionType, decimal amount)
        {
            return transactionType switch
            {
                CustomerBalanceTransactionType.Order =>
                    amount > 0 ? TransactionType.Buy : TransactionType.Sell,

                CustomerBalanceTransactionType.AccountingDocument =>
                    amount > 0 ? TransactionType.Document : TransactionType.DocumentDebit,

                CustomerBalanceTransactionType.Manual => TransactionType.ManualAdjustment,

                _ => TransactionType.Document
            };
        }

        /// <summary>
        /// Helper method to generate default description when none provided
        /// </summary>
        private string GetDefaultDescription(CustomerBalanceTransactionType transactionType, int? referenceId)
        {
            return transactionType switch
            {
                CustomerBalanceTransactionType.Order => $"معامله #{referenceId}",
                CustomerBalanceTransactionType.AccountingDocument => $"سند #{referenceId}",
                CustomerBalanceTransactionType.Manual => "تعدیل دستی",
                _ => "تراکنش"
            };
        }

        /// <summary>
        /// Get balance snapshot at a specific date
        /// </summary>
        public async Task<CustomerBalanceSnapshot> GetBalanceSnapshotAsync(int customerId, DateTime asOfDate)
        {
            var timeline = await GetCustomerTimelineAsync(customerId, DateTime.MinValue, asOfDate, null);

            var snapshot = new CustomerBalanceSnapshot
            {
                CustomerId = customerId,
                AsOfDate = asOfDate,
                Balances = timeline.FinalBalances,
                RecentTransactions = timeline.Transactions
                    .Where(t => t.TransactionDate >= asOfDate.AddDays(-30))
                    .OrderByDescending(t => t.TransactionDate)
                    .Take(10)
                    .ToList()
            };

            return snapshot;
        }

        /// <summary>
        /// GENIUS METHOD: Calculate balance as of a specific date
        /// This calculates what the balance was at the end of the specified date
        /// by summing all transactions up to and including that date
        /// </summary>
        private async Task<Dictionary<string, decimal>> CalculateBalanceAsOfDateAsync(int customerId, DateTime asOfDate)
        {
            var balances = new Dictionary<string, decimal>();

            // Get all orders up to the specified date
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.CustomerId == customerId && o.CreatedAt <= asOfDate)
                .ToListAsync();

            // Get all accounting documents up to the specified date
            var documents = await _context.AccountingDocuments
                .AsNoTracking()
                .Where(d => (d.PayerCustomerId == customerId || d.ReceiverCustomerId == customerId) &&
                           d.DocumentDate <= asOfDate &&
                           d.IsVerified)
                .ToListAsync();

            _logger.LogInformation($"Found {orders.Count} orders and {documents.Count} documents up to {asOfDate:yyyy-MM-dd}");

            // Process order transactions
            foreach (var order in orders)
            {
                // Initialize currency balances if not exists
                if (!balances.ContainsKey(order.FromCurrency.Code))
                    balances[order.FromCurrency.Code] = 0;
                if (!balances.ContainsKey(order.ToCurrency.Code))
                    balances[order.ToCurrency.Code] = 0;

                // Customer pays FromCurrency (decrease balance)
                balances[order.FromCurrency.Code] -= order.FromAmount;

                // Customer receives ToCurrency (increase balance)
                balances[order.ToCurrency.Code] += order.ToAmount;
            }

            // Process document transactions
            foreach (var doc in documents)
            {
                if (!balances.ContainsKey(doc.CurrencyCode))
                    balances[doc.CurrencyCode] = 0;

                if (doc.PayerCustomerId == customerId)
                {
                    // Customer is payer (decrease balance)
                    balances[doc.CurrencyCode] -= doc.Amount;
                }
                else if (doc.ReceiverCustomerId == customerId)
                {
                    // Customer is receiver (increase balance)
                    balances[doc.CurrencyCode] += doc.Amount;
                }
            }

            _logger.LogInformation($"Calculated balances as of {asOfDate:yyyy-MM-dd}: {string.Join(", ", balances.Select(kv => $"{kv.Key}={kv.Value:N2}"))}");

            return balances;
        }

        /// <summary>
        /// Get summary statistics for customer transactions
        /// Refactored: Uses timeline data to eliminate duplication
        /// </summary>
        public async Task<Dictionary<string, object>> GetCustomerStatsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var timeline = await GetCustomerTimelineAsync(customerId, fromDate, toDate, null);

            var totalVolume = timeline.Transactions
                .Where(t => t.Amount > 0)
                .GroupBy(t => t.CurrencyCode)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var stats = new Dictionary<string, object>
            {
                ["TotalTransactions"] = timeline.TotalTransactions,
                ["OrderTransactions"] = timeline.OrderCount,
                ["DocumentTransactions"] = timeline.DocumentCount,
                ["CurrenciesInvolved"] = timeline.CurrenciesInvolved.Count,
                ["TotalVolume"] = totalVolume,
                ["NetChanges"] = timeline.NetChanges,
                ["DateRange"] = new { From = timeline.FromDate, To = timeline.ToDate }
            };

            return stats;
        }

        /// <summary>
        /// Get transaction summary grouped by currency
        /// Refactored: Uses timeline data to eliminate duplication
        /// </summary>
        public async Task<Dictionary<string, CurrencyTransactionSummary>> GetCurrencyTransactionSummaryAsync(
            int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var timeline = await GetCustomerTimelineAsync(customerId, fromDate, toDate, null);

            var summary = timeline.Transactions
                .GroupBy(t => t.CurrencyCode)
                .ToDictionary(g => g.Key, g => new CurrencyTransactionSummary
                {
                    CurrencyCode = g.Key,
                    TotalDebits = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                    TotalCredits = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    TransactionCount = g.Count(),
                    OrderTransactions = g.Count(t => t.Type == TransactionType.Sell || t.Type == TransactionType.Buy),
                    DocumentTransactions = g.Count(t => t.Type == TransactionType.Document || t.Type == TransactionType.DocumentDebit),
                    NetChange = g.Sum(t => t.Amount),
                    LastTransactionDate = g.Max(t => t.TransactionDate)
                });

            return summary;
        }
    }

    /// <summary>
    /// Currency-specific transaction summary
    /// </summary>
    public class CurrencyTransactionSummary
    {
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public int TransactionCount { get; set; }
        public int OrderTransactions { get; set; }
        public int DocumentTransactions { get; set; }
        public decimal NetChange { get; set; }
        public DateTime LastTransactionDate { get; set; }

        public decimal TotalVolume => TotalDebits + TotalCredits;
        public string NetChangeStatus => NetChange >= 0 ? "افزایش" : "کاهش";
    }
}
