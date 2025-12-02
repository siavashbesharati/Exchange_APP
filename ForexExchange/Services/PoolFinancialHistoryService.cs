using ForexExchange.Models;
using ForexExchange.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ForexExchange.Services
{
    /// <summary>
    /// Pool Financial History Service - Database-driven timeline for Currency Pool transactions
    /// سرویس تاریخچه مالی پول - جدول زمانی مبتنی بر پایگاه داده برای تراکنش‌های داشبورد ها
    /// 
    /// Uses CurrencyPoolHistory table as the single source of truth
    /// از جدول CurrencyPoolHistory به عنوان منبع واحد حقیقت استفاده می‌کند
    /// </summary>
    public class PoolFinancialHistoryService : BaseFinancialHistoryService<PoolTimelineItem, PoolSummary>
    {
        public PoolFinancialHistoryService(ForexDbContext context) : base(context)
        {
        }

        #region Abstract Method Implementations

        protected override async Task<List<PoolTimelineItem>> GetTimelineItemsAsync(DateTime fromDate, DateTime toDate, object? filter = null)
        {
            // Filter MUST be CurrencyId (int) - NO CurrencyCode fallback!
            int? currencyId = null;
            
            if (filter is int id)
            {
                currencyId = id;
            }
            else if (filter is string)
            {
                // CurrencyCode is not supported - return empty list
                return new List<PoolTimelineItem>();
            }
            
            // Build query for CurrencyPoolHistory - EXCLUDE ONLY DELETED RECORDS FOR REPORTING
            var query = _context.CurrencyPoolHistory.Where(h => !h.IsDeleted);

            // Apply currency filter - use CurrencyId directly (no fallback to CurrencyCode)
            if (currencyId.HasValue)
            {
                // Use CurrencyId directly - this is why we did the refactoring!
                query = query.Where(h => h.CurrencyId == currencyId.Value);
            }

            // Apply date filter
            query = query.Where(h => h.TransactionDate >= fromDate && h.TransactionDate <= toDate);

            // Get history records ordered by date then id (oldest first)
            // Include Orders to get Customer name
            var historyRecords = await query
                .AsNoTracking()
                .Include(h => h.Currency)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            if (!historyRecords.Any())
                return new List<PoolTimelineItem>();

            // Get all order IDs that are referenced in history records
            var orderIds = historyRecords
                .Where(h => h.TransactionType == CurrencyPoolTransactionType.Order && h.ReferenceId.HasValue)
                .Select(h => h.ReferenceId.Value)
                .Distinct()
                .ToList();

            // Load orders with customers in one query
            var orders = new Dictionary<int, Order>();
            if (orderIds.Any())
            {
                var ordersList = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Customer)
                    .Where(o => orderIds.Contains(o.Id))
                    .ToListAsync();
                
                orders = ordersList.ToDictionary(o => o.Id);
            }

            var timelineItems = new List<PoolTimelineItem>();

           
            // Convert history records to timeline items
            foreach (var record in historyRecords)
            {
                // Get customer name from order if this is an Order transaction
                string customerName = string.Empty;
                if (record.TransactionType == CurrencyPoolTransactionType.Order && record.ReferenceId.HasValue)
                {
                    if (orders.TryGetValue(record.ReferenceId.Value, out var order))
                    {
                        customerName = order.Customer?.FullName ?? string.Empty;
                    }
                }

                var item = new PoolTimelineItem
                {
                    Id = record.Id, // Set the transaction ID for delete operations
                    Date = FormatGregorianDate(record.TransactionDate),
                    Time = FormatTime(record.TransactionDate),
                    TransactionType = record.TransactionType.ToString(),
                    Description = record.Description ?? GenerateTransactionDescription(record),
                    CurrencyId = record.CurrencyId ?? 0,
                    CurrencyCode = record.Currency != null ? record.Currency.Code : record.CurrencyCode, // Display from navigation
                    Amount = record.TransactionAmount,
                    Balance = record.BalanceAfter,
                    ReferenceId = record.ReferenceId,
                    CanNavigate = record.TransactionType == CurrencyPoolTransactionType.Order && record.ReferenceId.HasValue,
                    CustomerName = customerName
                };

                timelineItems.Add(item);
            }

            return timelineItems;
        }

        protected override async Task<PoolSummary> GetSummaryStatisticsAsync(object? filter = null)
        {
            // Filter MUST be CurrencyId (int) - NO CurrencyCode fallback!
            int? currencyId = null;
            
            if (filter is int id)
            {
                currencyId = id;
            }
            else if (filter is string)
            {
                // CurrencyCode is not supported - return empty summary
                return new PoolSummary
                {
                    TotalTransactions = 0,
                    TodayTransactions = 0,
                    CurrencyBalances = new Dictionary<string, decimal>(),
                    LastUpdateTime = DateTime.Now
                };
            }

            // Build query for CurrencyPoolHistory - EXCLUDE ONLY DELETED RECORDS FOR REPORTING
            var query = _context.CurrencyPoolHistory.Where(h => !h.IsDeleted);

            // Apply currency filter - use CurrencyId directly (no fallback to CurrencyCode)
            if (currencyId.HasValue)
            {
                // Use CurrencyId directly - this is why we did the refactoring!
                query = query.Where(h => h.CurrencyId == currencyId.Value);
            }

            var today = DateTime.Today;
            var totalTransactions = await query.CountAsync();
            var todayTransactions = await query.CountAsync(h => h.TransactionDate.Date == today);

            // Get current balance from latest record for each currency
            // Use CurrencyId grouping when available, fallback to CurrencyCode
            var latestBalancesQuery = _context.CurrencyPoolHistory
                .AsNoTracking()
                .Include(h => h.Currency)
                .Where(h => !h.IsDeleted);
            
            if (currencyId.HasValue)
            {
                // Use CurrencyId directly - this is why we did the refactoring!
                latestBalancesQuery = latestBalancesQuery.Where(h => h.CurrencyId == currencyId.Value);
            }
            
            var latestBalances = await latestBalancesQuery
                .Where(h => h.CurrencyId.HasValue)
                .GroupBy(h => h.CurrencyId.Value)
                .Select(g => new
                {
                    CurrencyId = g.Key,
                    CurrencyCode = g.First().Currency != null ? g.First().Currency.Code : g.First().CurrencyCode, // Display from navigation
                    Balance = g.OrderByDescending(h => h.TransactionDate)
                              .ThenByDescending(h => h.Id)
                              .First().BalanceAfter
                })
                .ToListAsync();

            return new PoolSummary
            {
                TotalTransactions = totalTransactions,
                TodayTransactions = todayTransactions,
                CurrencyBalances = latestBalances.ToDictionary(b => b.CurrencyCode, b => b.Balance),
                LastUpdateTime = DateTime.Now
            };
        }

        protected override string GenerateTransactionDescription(object transactionRecord)
        {
            if (transactionRecord is CurrencyPoolHistory record)
            {
                return record.TransactionType switch
                {
                    CurrencyPoolTransactionType.Order => $"معامله شماره {record.ReferenceId}",
                    CurrencyPoolTransactionType.ManualEdit => "ویرایش دستی موجودی",
                    _ => record.Description ?? "تراکنش نامشخص"
                };
            }
            return "نامشخص";
        }

        #endregion

        /// <summary>
        /// Gets currency pool financial timeline from CurrencyPoolHistory table
        /// دریافت جدول زمانی مالی داشبورد  از جدول CurrencyPoolHistory
        /// </summary>
        public async Task<List<PoolTimelineItem>> GetPoolTimelineAsync(
            object? currencyFilter = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            return await GetTimelineAsync(fromDate, toDate, currencyFilter);
        }

    

        /// <summary>
        /// Get pool summary statistics
        /// دریافت آمار خلاصه پول
        /// </summary>
        public async Task<PoolSummary> GetPoolSummaryAsync(object? currencyFilter = null)
        {
            return await GetSummaryAsync(currencyFilter);
        }
    }

    /// <summary>
    /// Pool Timeline Item for display
    /// آیتم جدول زمانی پول برای نمایش
    /// </summary>
    /// <summary>
    /// Pool Timeline Item for display
    /// آیتم جدول زمانی پول برای نمایش
    /// </summary>
    public class PoolTimelineItem : ITimelineItem
    {
        public long Id { get; set; } // Transaction ID for delete operations
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public int? ReferenceId { get; set; }
        public bool CanNavigate { get; set; }

        // Pool specific properties
        public string CurrencyCode { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public string CustomerName { get; set; } = string.Empty; // Customer name for Order transactions
    }

    /// <summary>
    /// Pool Summary Statistics
    /// آمار خلاصه پول
    /// </summary>
    public class PoolSummary : ISummaryStatistics
    {
        public int TotalTransactions { get; set; }
        public int TodayTransactions { get; set; }
        public Dictionary<string, decimal> CurrencyBalances { get; set; } = new();
        public DateTime LastUpdateTime { get; set; }
    }
}
