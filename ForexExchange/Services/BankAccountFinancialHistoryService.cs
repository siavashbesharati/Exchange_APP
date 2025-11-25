using ForexExchange.Models;
using ForexExchange.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ForexExchange.Services
{
    /// <summary>
    /// Bank Account Financial History Service - Database-driven timeline for Bank Account transactions
    /// سرویس تاریخچه مالی حساب بانکی - جدول زمانی مبتنی بر پایگاه داده برای تراکنش‌های حساب بانکی
    /// 
    /// Uses BankAccountBalanceHistory table as the single source of truth
    /// از جدول BankAccountBalanceHistory به عنوان منبع واحد حقیقت استفاده می‌کند
    /// </summary>
    public class BankAccountFinancialHistoryService : BaseFinancialHistoryService<BankAccountTimelineItem, BankAccountSummary>
    {
        public BankAccountFinancialHistoryService(ForexDbContext context) : base(context)
        {
        }

        #region Abstract Method Implementations

        protected override async Task<List<BankAccountTimelineItem>> GetTimelineItemsAsync(DateTime fromDate, DateTime toDate, object? filter = null)
        {
            var bankAccountId = filter as int?;
            
            // Build query for BankAccountBalanceHistory - EXCLUDE ONLY DELETED RECORDS FOR REPORTING
            var query = _context.BankAccountBalanceHistory.Where(h => !h.IsDeleted);

            // Apply bank account filter
            if (bankAccountId.HasValue)
            {
                query = query.Where(h => h.BankAccountId == bankAccountId.Value);
            }

            // Apply date filter
            query = query.Where(h => h.TransactionDate >= fromDate && h.TransactionDate <= toDate);

            var historyRecords = await query
                .AsNoTracking()
                .Include(h => h.BankAccount)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            if (!historyRecords.Any())
                return new List<BankAccountTimelineItem>();

            var timelineItems = new List<BankAccountTimelineItem>();

      

            // Convert history records to timeline items
            foreach (var record in historyRecords)
            {
                var item = new BankAccountTimelineItem
                {
                    Id = record.Id, // Set the transaction ID for delete operations
                    Date = FormatGregorianDate(record.TransactionDate),
                    Time = FormatTime(record.TransactionDate),
                    TransactionType = record.TransactionType.ToString(),
                    Description = record.Description ?? GenerateTransactionDescription(record),
                    BankAccountId = record.BankAccountId,
                    BankAccountName = GetCleanBankAccountName(record.BankAccount),
                    Amount = record.TransactionAmount,
                    Balance = record.BalanceAfter,
                    ReferenceId = record.ReferenceId,
                    CanNavigate = record.TransactionType == BankAccountTransactionType.Document && record.ReferenceId.HasValue
                };

                timelineItems.Add(item);
            }

            return timelineItems;
        }

        protected override async Task<BankAccountSummary> GetSummaryStatisticsAsync(object? filter = null)
        {
            var bankAccountId = filter as int?;
            
            var query = _context.BankAccountBalanceHistory.Where(h => !h.IsDeleted);

            if (bankAccountId.HasValue)
            {
                query = query.Where(h => h.BankAccountId == bankAccountId.Value);
            }

            var today = DateTime.Today;
            var totalTransactions = await query.CountAsync();
            var todayTransactions = await query.CountAsync(h => h.TransactionDate.Date == today);

            // Get current balance from latest record for each bank account
            var latestBalances = await _context.BankAccountBalanceHistory
                .AsNoTracking()
                .Where(h => !h.IsDeleted && (!bankAccountId.HasValue || h.BankAccountId == bankAccountId.Value))
                .Include(h => h.BankAccount)
                .GroupBy(h => h.BankAccountId)
                .Select(g => new
                {
                    BankAccountId = g.Key,
                    BankAccountName = g.First().BankAccount!.AccountHolderName + " - " + g.First().BankAccount!.BankName,
                    Balance = g.OrderByDescending(h => h.TransactionDate)
                              .ThenByDescending(h => h.Id)
                              .First().BalanceAfter
                })
                .ToListAsync();

            return new BankAccountSummary
            {
                TotalTransactions = totalTransactions,
                TodayTransactions = todayTransactions,
                AccountBalances = latestBalances.ToDictionary(
                    b => b.BankAccountId, 
                    b => (dynamic)new { Name = b.BankAccountName, Balance = b.Balance }
                ),
                LastUpdateTime = DateTime.Now
            };
        }

        protected override string GenerateTransactionDescription(object transactionRecord)
        {
            if (transactionRecord is BankAccountBalanceHistory record)
            {
                return record.TransactionType switch
                {
                    BankAccountTransactionType.Document => $"سند حسابداری شماره {record.ReferenceId}",
                    BankAccountTransactionType.ManualEdit => "ویرایش دستی موجودی",
                    _ => "تراکنش نامشخص"
                };
            }
            return "نامشخص";
        }

        #endregion

        /// <summary>
        /// Gets bank account financial timeline from BankAccountBalanceHistory table
        /// دریافت جدول زمانی مالی حساب بانکی از جدول BankAccountBalanceHistory
        /// </summary>
        public async Task<List<BankAccountTimelineItem>> GetBankAccountTimelineAsync(
            int? bankAccountId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            return await GetTimelineAsync(fromDate, toDate, bankAccountId);
        }

        /// <summary>
        /// Get transaction description based on type and data
        /// دریافت توضیحات تراکنش بر اساس نوع و داده‌ها
        /// </summary>
        private string GetTransactionDescription(BankAccountBalanceHistory record)
        {
            return record.TransactionType switch
            {
                BankAccountTransactionType.Document => $"سند حسابداری شماره {record.ReferenceId}",
                BankAccountTransactionType.ManualEdit => "ویرایش دستی موجودی",
                _ => "تراکنش نامشخص"
            };
        }

        /// <summary>
        /// Get clean bank account name without "سیستم صرافی"
        /// دریافت نام تمیز حساب بانکی بدون "سیستم صرافی"
        /// </summary>
        private string GetCleanBankAccountName(BankAccount? bankAccount)
        {
            if (bankAccount == null) return "نامشخص";
            
            var accountName = bankAccount.AccountHolderName == "سیستم صرافی" 
                ? bankAccount.BankName 
                : bankAccount.AccountHolderName;
                
            return (accountName ?? "نامشخص") + " (" + bankAccount.CurrencyCode + ")";
        }

        /// <summary>
        /// Get bank account summary statistics
        /// دریافت آمار خلاصه حساب بانکی
        /// </summary>
        public async Task<BankAccountSummary> GetBankAccountSummaryAsync(int? bankAccountId = null)
        {
            return await GetSummaryAsync(bankAccountId);
        }

        /// <summary>
        /// Get all bank accounts for dropdown/selection
        /// دریافت همه حساب‌های بانکی برای کشویی/انتخاب
        /// </summary>
        public async Task<List<BankAccountOption>> GetBankAccountOptionsAsync()
        {
            try
            {
                var bankAccounts = await _context.BankAccounts
                    .Where(ba => ba.IsActive) // Only active accounts
                    .OrderBy(ba => ba.BankName)
                    .ThenBy(ba => ba.AccountHolderName)
                    .Select(ba => new BankAccountOption
                    {
                        Id = ba.Id,
                        Name = ba.BankName + " - " + ba.CurrencyCode,
                        BankName = ba.BankName ?? "نامشخص"
                    })
                    .ToListAsync();

                return bankAccounts;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading bank account options: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Bank Account Timeline Item for display
    /// آیتم جدول زمانی حساب بانکی برای نمایش
    /// </summary>
    public class BankAccountTimelineItem : ITimelineItem
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

        // Bank Account specific properties
        public int BankAccountId { get; set; }
        public string BankAccountName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Bank Account Summary Statistics
    /// آمار خلاصه حساب بانکی
    /// </summary>
    public class BankAccountSummary : ISummaryStatistics
    {
        public int TotalTransactions { get; set; }
        public int TodayTransactions { get; set; }
        public Dictionary<int, dynamic> AccountBalances { get; set; } = new();
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Bank Account Option for dropdowns
    /// گزینه حساب بانکی برای کشویی‌ها
    /// </summary>
    public class BankAccountOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
    }
}
