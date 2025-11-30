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
            var currencyCode = filter as string;
            
            // Build query for CurrencyPoolHistory - EXCLUDE ONLY DELETED RECORDS FOR REPORTING
            var query = _context.CurrencyPoolHistory.Where(h => !h.IsDeleted);

            // Apply currency filter - prioritize CurrencyId over CurrencyCode
            if (!string.IsNullOrEmpty(currencyCode))
            {
                // Try to find CurrencyId from CurrencyCode for better performance
                var currency = await _context.Currencies
                    .FirstOrDefaultAsync(c => (c.Code ?? "").ToUpperInvariant().Trim() == currencyCode.ToUpperInvariant().Trim());
                
                if (currency != null)
                {
                    // Use CurrencyId if available, otherwise fallback to CurrencyCode
                    query = query.Where(h => h.CurrencyId == currency.Id || 
                                            (h.CurrencyId == null && h.CurrencyCode == currencyCode));
                }
                else
                {
                    // Fallback to CurrencyCode if currency not found
                    query = query.Where(h => h.CurrencyCode == currencyCode);
                }
            }

            // Apply date filter
            query = query.Where(h => h.TransactionDate >= fromDate && h.TransactionDate <= toDate);

            // Get history records ordered by date then id (oldest first)
            var historyRecords = await query
                .AsNoTracking()
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            if (!historyRecords.Any())
                return new List<PoolTimelineItem>();

            var timelineItems = new List<PoolTimelineItem>();

           
            // Convert history records to timeline items
            foreach (var record in historyRecords)
            {
                var item = new PoolTimelineItem
                {
                    Id = record.Id, // Set the transaction ID for delete operations
                    Date = FormatGregorianDate(record.TransactionDate),
                    Time = FormatTime(record.TransactionDate),
                    TransactionType = record.TransactionType.ToString(),
                    Description = record.Description ?? GenerateTransactionDescription(record),
                    CurrencyCode = record.CurrencyCode,
                    Amount = record.TransactionAmount,
                    Balance = record.BalanceAfter,
                    ReferenceId = record.ReferenceId,
                    CanNavigate = record.TransactionType == CurrencyPoolTransactionType.Order && record.ReferenceId.HasValue
                };

                timelineItems.Add(item);
            }

            return timelineItems;
        }

        protected override async Task<PoolSummary> GetSummaryStatisticsAsync(object? filter = null)
        {
            var currencyCode = filter as string;
            
            var query = _context.CurrencyPoolHistory.Where(h => !h.IsDeleted);

            if (!string.IsNullOrEmpty(currencyCode))
            {
                // Try to find CurrencyId from CurrencyCode for better performance
                var currency = await _context.Currencies
                    .FirstOrDefaultAsync(c => (c.Code ?? "").ToUpperInvariant().Trim() == currencyCode.ToUpperInvariant().Trim());
                
                if (currency != null)
                {
                    // Use CurrencyId if available, otherwise fallback to CurrencyCode
                    query = query.Where(h => h.CurrencyId == currency.Id || 
                                            (h.CurrencyId == null && h.CurrencyCode == currencyCode));
                }
                else
                {
                    // Fallback to CurrencyCode if currency not found
                    query = query.Where(h => h.CurrencyCode == currencyCode);
                }
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
            
            if (!string.IsNullOrEmpty(currencyCode))
            {
                var currency = await _context.Currencies
                    .FirstOrDefaultAsync(c => (c.Code ?? "").ToUpperInvariant().Trim() == currencyCode.ToUpperInvariant().Trim());
                
                if (currency != null)
                {
                    latestBalancesQuery = latestBalancesQuery.Where(h => 
                        h.CurrencyId == currency.Id || 
                        (h.CurrencyId == null && h.CurrencyCode == currencyCode));
                }
                else
                {
                    latestBalancesQuery = latestBalancesQuery.Where(h => h.CurrencyCode == currencyCode);
                }
            }
            
            var latestBalances = await latestBalancesQuery
                .GroupBy(h => h.CurrencyId.HasValue && h.Currency != null ? h.Currency.Code : h.CurrencyCode)
                .Select(g => new
                {
                    CurrencyCode = g.Key,
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
            string? currencyCode = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            return await GetTimelineAsync(fromDate, toDate, currencyCode);
        }

    

        /// <summary>
        /// Get pool summary statistics
        /// دریافت آمار خلاصه پول
        /// </summary>
        public async Task<PoolSummary> GetPoolSummaryAsync(string? currencyCode = null)
        {
            return await GetSummaryAsync(currencyCode);
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
