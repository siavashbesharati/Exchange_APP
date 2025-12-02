using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using DNTPersianUtils.Core;
using ForexExchange.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Extensions;
using ForexExchange.Helpers;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Programmer")]
    public class DatabaseController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICurrencyPoolService _currencyPoolService;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly INotificationHub _notificationHub;
        private readonly UserManager<ApplicationUser> _userManager;

        public DatabaseController(ForexDbContext context, IWebHostEnvironment environment,
            ICurrencyPoolService currencyPoolService, ICentralFinancialService centralFinancialService,
            INotificationHub notificationHub, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _environment = environment;
            _currencyPoolService = currencyPoolService;
            _centralFinancialService = centralFinancialService;
            _notificationHub = notificationHub;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            var model = new DatabaseManagementViewModel
            {
                CustomersCount = _context.Customers.Where(c => !c.IsSystem).Count(),
                OrdersCount = _context.Orders.Count(),
                CurrencyPoolsCount = _context.CurrencyPools.Count(),
                // TODO: Replace with AccountingDocument counts
                TransactionsCount = 0, // _context.Transactions.Count(),
                ExchangeRatesCount = _context.ExchangeRates.Count(),
                AccountingDocumentsCount = 0 // _context.Receipts.Count()
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyPendingMigrations()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Check if there are pending migrations
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();

                if (!pendingMigrations.Any())
                {
                    TempData["SuccessMessage"] = "✅ پایگاه داده به‌روز است. هیچ migration در حال انتظاری وجود ندارد.";
                    return RedirectToAction(nameof(Index));
                }

                // Log the operation
                var migrationList = string.Join(", ", pendingMigrations);
                _context.AdminActivities.Add(new AdminActivity
                {
                    AdminUserId = currentUser?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.BulkOperation,
                    Description = $"Applying {pendingMigrations.Count()} pending migrations: {migrationList}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                // Apply migrations
                await _context.Database.MigrateAsync();
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ {pendingMigrations.Count()} migration با موفقیت اعمال شد: {migrationList}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _context.AdminActivities.Add(new AdminActivity
                {
                    AdminUserId = (await _userManager.GetUserAsync(User))?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.BulkOperation,
                    Description = $"Failed to apply migrations: {ex.Message}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = $"خطا در اعمال migration: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public IActionResult CreateBackup()
        {
            try
            {
                var now = DateTime.Now;
                var backupFileName = $"Taban_Backup_{now.GetPersianYear()}-{now.GetPersianMonth()}-{now.GetPersianDayOfMonth()}-{now.Hour}-{now.Minute}.tbn";
                var backupPath = Path.Combine(_environment.WebRootPath, "backups");

                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                var fullBackupPath = Path.Combine(backupPath, backupFileName);

                // Create SQLite backup by copying the database file
                var connectionString = _context.Database.GetConnectionString();
                var dbPath = connectionString?.Replace("Data Source=", "").Replace(";", "");

                if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
                {
                    System.IO.File.Copy(dbPath, fullBackupPath, true);
                }

                TempData["Success"] = $"پشتیبان‌گیری با موفقیت ایجاد شد: {backupFileName}";

                // Return the file directly for download
                var fileBytes = System.IO.File.ReadAllBytes(fullBackupPath);
                return File(fileBytes, "application/octet-stream", backupFileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در ایجاد پشتیبان: {ex.Message}";
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> TestPoolHistory()
        {
            try
            {
                var historyCount = await _context.CurrencyPoolHistory.CountAsync();
                var orderTransactions = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.Order)
                    .Take(10)
                    .Select(h => new
                    {
                        h.Id,
                        h.CurrencyCode,
                        h.TransactionType,
                        h.ReferenceId,
                        h.TransactionAmount,
                        h.Description,
                        h.TransactionDate
                    })
                    .ToListAsync();

                return Json(new
                {
                    totalHistoryRecords = historyCount,
                    orderTransactions = orderTransactions,
                    orderCount = orderTransactions.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult DownloadBackup(string fileName)
        {
            try
            {
                var backupPath = Path.Combine(_environment.WebRootPath, "backups", fileName);

                if (!System.IO.File.Exists(backupPath))
                {
                    TempData["Error"] = "فایل پشتیبان یافت نشد";
                    return RedirectToAction("Index");
                }

                var fileBytes = System.IO.File.ReadAllBytes(backupPath);
                return File(fileBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در دانلود فایل: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        public async Task<IActionResult> RestoreDatabase(IFormFile backupFile)
        {
            if (backupFile == null || backupFile.Length == 0)
            {
                TempData["Error"] = "لطفاً فایل پشتیبان را انتخاب کنید";
                return RedirectToAction("Index");
            }

            try
            {
                // Create automatic backup before restore (file copy)
                var now = DateTime.Now;
                var backupFileName = $"-Auto-Taban_Backup_{now.GetPersianYear()}-{now.GetPersianMonth()}-{now.GetPersianDayOfMonth()}-{now.Hour}-{now.Minute}.tbn";
                var backupPath = Path.Combine(_environment.WebRootPath, "backups");
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                var autoBackupPath = Path.Combine(backupPath, backupFileName);
                var connectionString = _context.Database.GetConnectionString();
                var dbPath = connectionString?.Replace("Data Source=", "").Replace(";", "");

                if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
                {
                    System.IO.File.Copy(dbPath, autoBackupPath, true);
                }

                // Save uploaded file temporarily
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await backupFile.CopyToAsync(stream);
                }

                // Use SQLite backup API to copy contents from uploaded DB into the live database
                // This avoids deleting/replacing the file while it's in use
                var busyDest = $"Data Source={dbPath};Cache=Shared";
                var busySrc = $"Data Source={tempPath};Mode=ReadOnly;Cache=Shared";

                using (var dest = new Microsoft.Data.Sqlite.SqliteConnection(busyDest))
                using (var src = new Microsoft.Data.Sqlite.SqliteConnection(busySrc))
                {
                    await dest.OpenAsync();
                    await src.OpenAsync();

                    // Set busy timeout via PRAGMA on both connections (connection string keyword not supported)
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=5000;";
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = src.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=5000;";
                        cmd.ExecuteNonQuery();
                    }

                    // Ensure WAL is checkpointed to reduce locks
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        cmd.ExecuteNonQuery();
                    }
                    // Temporarily disable foreign keys during restore to avoid temporary constraint errors
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA foreign_keys=OFF;";
                        cmd.ExecuteNonQuery();
                    }

                    // Copy database content
                    src.BackupDatabase(dest);

                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA foreign_keys=ON;";
                        cmd.ExecuteNonQuery();
                    }
                }

                // Cleanup temp file with a few retries; ignore failures
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            System.IO.File.Delete(tempPath);
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(200);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            await Task.Delay(200);
                        }
                    }
                }
                catch { /* ignore cleanup issues */ }

                _context.ChangeTracker.Clear();

                TempData["Success"] = $"بازیابی پایگاه داده با موفقیت انجام شد. پشتیبان خودکار ایجاد شد: {backupFileName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازیابی پایگاه داده: {ex.Message}";
            }

            return RedirectToAction("Index");
        }



        [HttpPost]
        public async Task<IActionResult> CreateManualCustomerBalanceHistory(
            int customerId,
            int currencyId,
            decimal amount,
            string reason,
            DateTime transactionDate)
        {
            try
            {
                // Validate inputs
                if (customerId <= 0)
                {
                    TempData["Error"] = "لطفاً مشتری معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }

                if (currencyId <= 0)
                {
                    TempData["Error"] = "لطفاً ارز معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "لطفاً دلیل تراکنش را وارد کنید";
                    return RedirectToAction("Index");
                }

                // Get customer name for display
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                var customerName = customer?.FullName ?? $"مشتری {customerId}";

                // Resolve CurrencyId from CurrencyCode - use CurrencyId directly (this is why we did the refactoring!)
                var currency = await _context.Currencies
                    .FindAsync(currencyId);
                if (currency == null)
                {
                    TempData["Error"] = $"ارز با کد {currency.Code} یافت نشد";
                    return RedirectToAction("Index");
                }

                // Get current user for notification exclusion
                var currentUser = await _userManager.GetUserAsync(User);

                // Create the manual history record with notification handling in service layer
                // Use CurrencyId directly - this is why we did the refactoring!
                await _centralFinancialService.CreateManualCustomerBalanceHistoryAsync(
                    customerId: customerId,
                    currencyId: currency.Id,
                    amount: amount,
                    reason: reason,
                    transactionDate: transactionDate,
                    performedBy: "Database Admin",
                    performingUserId: currentUser?.Id
                );

                var summary = new[]
                {
                    "✅ رکورد دستی تاریخچه موجودی ایجاد شد",
                    $"👤 مشتری: {customerName}",
                    $"💰 مبلغ: {amount:N2} {currency.Code}",
                    $"📅 تاریخ تراکنش: {transactionDate:yyyy-MM-dd}",
                    $"📝 دلیل: {reason}",
                    ""
                };

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تراکنش دستی با موفقیت ثبت شد" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در ایجاد رکورد دستی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در ایجاد رکورد دستی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManualCustomerBalanceHistory(long transactionId)
        {
            try
            {
                // Find the manual transaction record
                var transaction = await _context.CustomerBalanceHistory
                    .Include(h => h.Customer)
                    .Include(h => h.Currency)
                    .FirstOrDefaultAsync(h => h.Id == transactionId &&
                                           h.TransactionType == CustomerBalanceTransactionType.Manual);

                if (transaction == null)
                {
                    // Check if this is an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, error = "تراکنش دستی یافت نشد یا این تراکنش قابل حذف نیست" });
                    }

                    TempData["Error"] = "تراکنش دستی یافت نشد یا این تراکنش قابل حذف نیست";
                    return RedirectToAction("Index");
                }

                var customerName = transaction.Customer?.FullName ?? $"مشتری {transaction.CustomerId}";
                var amount = transaction.TransactionAmount;
                var currencyCode = transaction.Currency != null ? transaction.Currency.Code : transaction.CurrencyCode; // Display from navigation

                // Get current user for notification exclusion
                var currentUser = await _userManager.GetUserAsync(User);

                // Delete the transaction and recalculate balances with notification handling in service layer
                await _centralFinancialService.DeleteManualCustomerBalanceHistoryAsync(transactionId, "Database Admin", currentUser?.Id);

                var summary = new[]
                {
                    "✅ تعدیل دستی با موفقیت حذف شد",
                    $"👤 مشتری: {customerName}",
                    $"💰 مبلغ حذف شده: {amount:N2} {currencyCode}",
                    "",
                    "🔄 موجودی‌ها بازمحاسبه شدند"
                };

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تعدیل دستی با موفقیت حذف شد و موجودی‌ها بازمحاسبه شدند" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در حذف تعدیل دستی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در حذف تعدیل دستی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateNotesAndDescriptions()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";
                var logMessages = new List<string>();

                logMessages.Add("=== UPDATING NOTES AND DESCRIPTIONS ===");
                logMessages.Add($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add($"Performed by: {performedBy}");
                logMessages.Add("");

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // STEP 1: Load all data
                var orders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => !o.IsDeleted)
                    .ToListAsync();

                var documents = await _context.AccountingDocuments
                    .Include(d => d.PayerCustomer)
                    .Include(d => d.ReceiverCustomer)
                    .Include(d => d.PayerBankAccount)
                    .Include(d => d.ReceiverBankAccount)
                    .Include(d => d.Currency)
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();

                var customerBalanceHistory = await _context.CustomerBalanceHistory
                    .Where(h => !h.IsDeleted)
                    .ToListAsync();
                    
                var bankAccountBalanceHistory = await _context.BankAccountBalanceHistory
                    .Where(h => !h.IsDeleted)
                    .ToListAsync();
                    
                var poolHistoryRecords = await _context.CurrencyPoolHistory
                    .Where(h => !h.IsDeleted && h.TransactionType == CurrencyPoolTransactionType.Order)
                    .ToListAsync();

                // STEP 2: Update CurrencyPoolHistory and CustomerBalanceHistory for Orders
                logMessages.Add("STEP 2: Updating CurrencyPoolHistory and CustomerBalanceHistory for Orders...");
                int poolUpdated = 0;
                foreach (var historyRecord in poolHistoryRecords)
                {
                    if (!historyRecord.ReferenceId.HasValue) continue;
                    
                    var order = orders.FirstOrDefault(o => o.Id == historyRecord.ReferenceId.Value);
                    if (order != null)
                    {
                        var generatedDescription = HistoryDescriptionHelper.GenerateOrderDescription(order);
                        historyRecord.Description = generatedDescription;
                        
                        // Update CustomerBalanceHistory records for this order
                        var customerHistoryRecords = customerBalanceHistory
                            .Where(h => h.ReferenceId == order.Id && h.TransactionType == CustomerBalanceTransactionType.Order)
                            .ToList();
                            
                        foreach (var customerHistory in customerHistoryRecords)
                        {
                            customerHistory.Description = generatedDescription;
                            customerHistory.Note = generatedDescription; // Note with capital N
                        }
                        
                        poolUpdated++;
                    }
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Updated {poolUpdated} CurrencyPoolHistory and CustomerBalanceHistory records for Orders");

                // STEP 3: Update CustomerBalanceHistory and BankAccountBalanceHistory for Documents
                logMessages.Add("");
                logMessages.Add("STEP 3: Updating CustomerBalanceHistory and BankAccountBalanceHistory for Documents...");
                var customerHistoryRecordsForDocuments = customerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument && !h.IsDeleted)
                    .ToList();
                    
                int docUpdated = 0;
                foreach (var docHistory in customerHistoryRecordsForDocuments)
                {
                    if (!docHistory.ReferenceId.HasValue) continue;
                    
                    var document = documents.FirstOrDefault(d => d.Id == docHistory.ReferenceId.Value);
                    if (document != null)
                    {
                        var role = docHistory.TransactionAmount > 0 ? "Payer" : "Receiver";
                        var note = HistoryDescriptionHelper.GenerateDocumentNote(document);
                        var description = HistoryDescriptionHelper.GenerateDocumentDescription(document, role);
                        
                        docHistory.Note = note;
                        docHistory.Description = description;
                        
                        // Update BankAccountBalanceHistory (only Description, no Note property)
                        var bankHistoryRecord = bankAccountBalanceHistory
                            .FirstOrDefault(h => h.ReferenceId == docHistory.ReferenceId.Value);
                        if (bankHistoryRecord != null)
                        {
                            bankHistoryRecord.Description = description;
                        }
                        
                        docUpdated++;
                    }
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Updated {docUpdated} CustomerBalanceHistory and BankAccountBalanceHistory records for Documents");

                // STEP 4: Update Manual transactions
                logMessages.Add("");
                logMessages.Add("STEP 4: Updating Manual transactions...");
                var manualHistoryRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted)
                    .ToListAsync();

                int manualRecordsUpdated = 0;
                foreach (var history in manualHistoryRecords)
                {
                    // Use helper to generate English descriptions for manual adjustments
                    var description = HistoryDescriptionHelper.GenerateManualDescription(
                        history.Description ?? "Manual adjustment",
                        history.TransactionAmount,
                        history.CurrencyCode ?? "");

                    var note = $"Manual Adjustment - Amount: {history.TransactionAmount.FormatCurrency(history.CurrencyCode ?? "")} {history.CurrencyCode ?? ""}";
                    if (!string.IsNullOrWhiteSpace(history.Description))
                    {
                        note += $" | Reason: {history.Description}";
                    }
                    if (!string.IsNullOrWhiteSpace(history.TransactionNumber))
                    {
                        note += $" | Transaction ID: {history.TransactionNumber}";
                    }

                    history.Description = description;
                    history.Note = note;
                    manualRecordsUpdated++;
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Updated {manualRecordsUpdated} manual adjustment records");

                await dbTransaction.CommitAsync();

                TempData["Success"] = string.Join("<br/>", logMessages);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بروزرسانی یادداشت‌ها: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> RebuildAllFinancialBalances()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";

                // Call the service method instead of local implementation
                await _centralFinancialService.RebuildAllFinancialBalancesAsync(performedBy);

                TempData["Success"] = "بازسازی کامل موجودی‌های مالی با زنجیره‌های منسجم موجودی با موفقیت انجام شد!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازسازی موجودی‌های مالی: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FreezeAllOrdersAndDocuments()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";

                var ordersFrozen = await _centralFinancialService.FreezeAllOrdersAndDocumentsAsync(performedBy);

                var freezeTimestamp = DateTime.UtcNow;

                var manualPoolSoftDeleted = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(h => h.IsDeleted, _ => true)
                        .SetProperty(h => h.DeletedAt, _ => freezeTimestamp)
                        .SetProperty(h => h.DeletedBy, _ => performedBy));

                await _centralFinancialService.RebuildAllFinancialBalancesAsync(performedBy);

                var successMessage = $"داشبورد با موفقیت ریست شد.<br/>";

                return Json(new
                {
                    success = true,
                    message = successMessage,
                    ordersFrozen,
                    manualPoolSoftDeleted
                });
            }
            catch (Exception ex)
            {
                var errorMessage = $"خطا در ریست داشبورد  : {ex.Message}";

                return Json(new { success = false, error = errorMessage });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateVapidConfigurations()
        {
            try
            {
                // 1. حذف کلیدهای قبلی
                var oldConfigs = await _context.VapidConfigurations.ToListAsync();
                var deletedCount = oldConfigs.Count;
                if (deletedCount > 0)
                {
                    _context.VapidConfigurations.RemoveRange(oldConfigs);
                }

                // 2. ساخت کلید جدید
                var newKeys = WebPush.VapidHelper.GenerateVapidKeys();
                var newConfig = new VapidConfiguration
                {
                    PublicKey = newKeys.PublicKey,
                    PrivateKey = newKeys.PrivateKey,
                    CreatedAt = DateTime.UtcNow
                };
                _context.VapidConfigurations.Add(newConfig);

                // 3. ذخیره تغییرات در دیتابیس
                await _context.SaveChangesAsync();

                TempData["Success"] = deletedCount > 0
                    ? $"کلیدهای قدیمی ({deletedCount} مورد) حذف و یک کلید جدید VAPID ساخته شد: {newKeys.PublicKey}"
                    : $"هیچ کلید قدیمی وجود نداشت، اما یک کلید جدید VAPID ساخته شد: {newKeys.PublicKey}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازسازی کلیدهای VAPID: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllPushSubscriptions()
        {
            try
            {
                var subscriptions = await _context.PushSubscriptions.ToListAsync();
                var deletedCount = subscriptions.Count;

                if (deletedCount > 0)
                {
                    _context.PushSubscriptions.RemoveRange(subscriptions);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = deletedCount > 0
                    ? $"تمام اشتراک‌های اعلان فشاری ({deletedCount} مورد) حذف شدند."
                    : "هیچ اشتراک فعالی برای حذف وجود نداشت.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در حذف اشتراک‌های اعلان فشاری: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// حذف رکوردهای تکراری در BankAccountBalanceHistory و CustomerBalanceHistory
        /// معیار تکراری بودن: TransactionAmount, TransactionDate, TransactionType
        /// فقط اولین رکورد (با کمترین Id) باقی می‌ماند
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDuplicateHistoryRecords()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";
                var logMessages = new List<string>();
                var deletedTimestamp = DateTime.UtcNow;

                logMessages.Add("=== حذف رکوردهای تکراری تاریخچه ===");
                logMessages.Add($"شروع: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add($"اجرا شده توسط: {performedBy}");
                logMessages.Add("");

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // ============================================================
                // 1. حذف تکراری‌های BankAccountBalanceHistory
                // معیار: TransactionAmount, TransactionDate, TransactionType
                // ============================================================
                logMessages.Add("مرحله 1: بررسی BankAccountBalanceHistory...");

                // بارگذاری تمام رکوردهای غیرحذف‌شده
                var allBankRecords = await _context.BankAccountBalanceHistory
                    .Where(h => !h.IsDeleted)
                    .OrderBy(h => h.Id)
                    .ToListAsync();

                // گروه‌بندی و پیدا کردن تکراری‌ها
                var bankGroups = allBankRecords
                    .GroupBy(h => new
                    {
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.TransactionType
                    })
                    .Where(g => g.Count() > 1)
                    .ToList();

                int bankDeletedCount = 0;
                foreach (var group in bankGroups)
                {
                    // نگه داشتن اولین رکورد (با کمترین Id) و حذف بقیه
                    var recordsToDelete = group.OrderBy(r => r.Id).Skip(1).ToList();

                    foreach (var record in recordsToDelete)
                    {
                        record.IsDeleted = true;
                        record.DeletedAt = deletedTimestamp;
                        record.DeletedBy = performedBy;
                        bankDeletedCount++;
                    }
                }

                if (bankDeletedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    logMessages.Add($"✓ {bankDeletedCount} رکورد تکراری از BankAccountBalanceHistory حذف شد");
                }
                else
                {
                    logMessages.Add("✓ هیچ رکورد تکراری در BankAccountBalanceHistory یافت نشد");
                }

                // ============================================================
                // 2. حذف تکراری‌های CustomerBalanceHistory
                // معیار: TransactionAmount, TransactionDate, TransactionType
                // ============================================================
                logMessages.Add("");
                logMessages.Add("مرحله 2: بررسی CustomerBalanceHistory...");

                // بارگذاری تمام رکوردهای غیرحذف‌شده
                var allCustomerRecords = await _context.CustomerBalanceHistory
                    .Where(h => !h.IsDeleted)
                    .OrderBy(h => h.Id)
                    .ToListAsync();

                // گروه‌بندی و پیدا کردن تکراری‌ها
                var customerGroups = allCustomerRecords
                    .GroupBy(h => new
                    {
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.TransactionType
                    })
                    .Where(g => g.Count() > 1)
                    .ToList();

                int customerDeletedCount = 0;
                foreach (var group in customerGroups)
                {
                    // نگه داشتن اولین رکورد (با کمترین Id) و حذف بقیه
                    var recordsToDelete = group.OrderBy(r => r.Id).Skip(1).ToList();

                    foreach (var record in recordsToDelete)
                    {
                        record.IsDeleted = true;
                        record.DeletedAt = deletedTimestamp;
                        record.DeletedBy = performedBy;
                        customerDeletedCount++;
                    }
                }

                if (customerDeletedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    logMessages.Add($"✓ {customerDeletedCount} رکورد تکراری از CustomerBalanceHistory حذف شد");
                }
                else
                {
                    logMessages.Add("✓ هیچ رکورد تکراری در CustomerBalanceHistory یافت نشد");
                }

                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== عملیات با موفقیت انجام شد ===");
                logMessages.Add($"مجموع رکوردهای حذف شده: {bankDeletedCount + customerDeletedCount}");
                logMessages.Add($"  - BankAccountBalanceHistory: {bankDeletedCount}");
                logMessages.Add($"  - CustomerBalanceHistory: {customerDeletedCount}");

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        message = $"حذف تکراری‌ها با موفقیت انجام شد. {bankDeletedCount + customerDeletedCount} رکورد حذف شد.",
                        bankDeletedCount,
                        customerDeletedCount,
                        totalDeleted = bankDeletedCount + customerDeletedCount
                    });
                }

                TempData["Success"] = string.Join("<br/>", logMessages);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در حذف تکراری‌ها: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در حذف تکراری‌ها: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Populates CurrencyId fields from CurrencyCode in all tables
        /// This migration script fills CurrencyId for records that have CurrencyCode but missing CurrencyId
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PopulateCurrencyIds()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";
                var logMessages = new List<string>();

                logMessages.Add("=== POPULATING CurrencyId FROM CurrencyCode ===");
                logMessages.Add($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add($"Performed by: {performedBy}");
                logMessages.Add("");

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // STEP 1: Populate CustomerBalances.CurrencyId
                logMessages.Add("STEP 1: Populating CustomerBalances.CurrencyId...");
                var customerBalancesUpdated = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE CustomerBalances 
                    SET CurrencyId = (SELECT Id FROM Currencies WHERE UPPER(TRIM(Code)) = UPPER(TRIM(CustomerBalances.CurrencyCode)))
                    WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
                ");
                logMessages.Add($"✓ Updated {customerBalancesUpdated} CustomerBalances records");

                // STEP 2: Populate CustomerBalanceHistory.CurrencyId
                logMessages.Add("");
                logMessages.Add("STEP 2: Populating CustomerBalanceHistory.CurrencyId...");
                var customerHistoryUpdated = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE CustomerBalanceHistory 
                    SET CurrencyId = (SELECT Id FROM Currencies WHERE UPPER(TRIM(Code)) = UPPER(TRIM(CustomerBalanceHistory.CurrencyCode)))
                    WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
                ");
                logMessages.Add($"✓ Updated {customerHistoryUpdated} CustomerBalanceHistory records");

                // STEP 3: Populate CurrencyPoolHistory.CurrencyId
                logMessages.Add("");
                logMessages.Add("STEP 3: Populating CurrencyPoolHistory.CurrencyId...");
                var poolHistoryUpdated = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE CurrencyPoolHistory 
                    SET CurrencyId = (SELECT Id FROM Currencies WHERE UPPER(TRIM(Code)) = UPPER(TRIM(CurrencyPoolHistory.CurrencyCode)))
                    WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
                ");
                logMessages.Add($"✓ Updated {poolHistoryUpdated} CurrencyPoolHistory records");

                // STEP 4: Populate AccountingDocuments.CurrencyId
                logMessages.Add("");
                logMessages.Add("STEP 4: Populating AccountingDocuments.CurrencyId...");
                var documentsUpdated = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE AccountingDocuments 
                    SET CurrencyId = (SELECT Id FROM Currencies WHERE UPPER(TRIM(Code)) = UPPER(TRIM(AccountingDocuments.CurrencyCode)))
                    WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
                ");
                logMessages.Add($"✓ Updated {documentsUpdated} AccountingDocuments records");

                // STEP 5: Populate BankAccounts.CurrencyId
                logMessages.Add("");
                logMessages.Add("STEP 5: Populating BankAccounts.CurrencyId...");
                var bankAccountsUpdated = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE BankAccounts 
                    SET CurrencyId = (SELECT Id FROM Currencies WHERE UPPER(TRIM(Code)) = UPPER(TRIM(BankAccounts.CurrencyCode)))
                    WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
                ");
                logMessages.Add($"✓ Updated {bankAccountsUpdated} BankAccounts records");

                // STEP 6: Populate BankAccountBalances.CurrencyId
                logMessages.Add("");
                logMessages.Add("STEP 6: Populating BankAccountBalances.CurrencyId...");
                var bankAccountBalancesUpdated = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE BankAccountBalances 
                    SET CurrencyId = (SELECT Id FROM Currencies WHERE UPPER(TRIM(Code)) = UPPER(TRIM(BankAccountBalances.CurrencyCode)))
                    WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
                ");
                logMessages.Add($"✓ Updated {bankAccountBalancesUpdated} BankAccountBalances records");

                // STEP 7: Check for records that couldn't be updated (CurrencyCode not found in Currencies table)
                logMessages.Add("");
                logMessages.Add("STEP 7: Checking for records with invalid CurrencyCode...");

                var invalidCustomerBalances = await _context.CustomerBalances
                    .Where(cb => cb.CurrencyId == null && !string.IsNullOrEmpty(cb.CurrencyCode))
                    .Select(cb => cb.CurrencyCode)
                    .Distinct()
                    .ToListAsync();

                var invalidCustomerHistory = await _context.CustomerBalanceHistory
                    .Where(h => h.CurrencyId == null && !string.IsNullOrEmpty(h.CurrencyCode))
                    .Select(h => h.CurrencyCode)
                    .Distinct()
                    .ToListAsync();

                var invalidPoolHistory = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyId == null && !string.IsNullOrEmpty(h.CurrencyCode))
                    .Select(h => h.CurrencyCode)
                    .Distinct()
                    .ToListAsync();

                var invalidDocuments = await _context.AccountingDocuments
                    .Where(d => d.CurrencyId == null && !string.IsNullOrEmpty(d.CurrencyCode))
                    .Select(d => d.CurrencyCode)
                    .Distinct()
                    .ToListAsync();

                var invalidBankAccounts = await _context.BankAccounts
                    .Where(ba => ba.CurrencyId == null && !string.IsNullOrEmpty(ba.CurrencyCode))
                    .Select(ba => ba.CurrencyCode)
                    .Distinct()
                    .ToListAsync();

                var invalidBankAccountBalances = await _context.BankAccountBalances
                    .Where(bab => bab.CurrencyId == null && !string.IsNullOrEmpty(bab.CurrencyCode))
                    .Select(bab => bab.CurrencyCode)
                    .Distinct()
                    .ToListAsync();

                var allInvalidCodes = invalidCustomerBalances
                    .Union(invalidCustomerHistory)
                    .Union(invalidPoolHistory)
                    .Union(invalidDocuments)
                    .Union(invalidBankAccounts)
                    .Union(invalidBankAccountBalances)
                    .Distinct()
                    .ToList();

                if (allInvalidCodes.Any())
                {
                    logMessages.Add($"⚠️ Warning: Found {allInvalidCodes.Count} invalid CurrencyCode(s) that don't exist in Currencies table:");
                    foreach (var code in allInvalidCodes)
                    {
                        logMessages.Add($"   - {code}");
                    }
                }
                else
                {
                    logMessages.Add("✓ All CurrencyCode values were successfully matched to CurrencyId");
                }

                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== MIGRATION COMPLETED SUCCESSFULLY ===");
                var totalUpdated = customerBalancesUpdated + customerHistoryUpdated + poolHistoryUpdated + documentsUpdated + bankAccountsUpdated + bankAccountBalancesUpdated;
                logMessages.Add($"Total records updated: {totalUpdated}");
                logMessages.Add($"  - CustomerBalances: {customerBalancesUpdated}");
                logMessages.Add($"  - CustomerBalanceHistory: {customerHistoryUpdated}");
                logMessages.Add($"  - CurrencyPoolHistory: {poolHistoryUpdated}");
                logMessages.Add($"  - AccountingDocuments: {documentsUpdated}");
                logMessages.Add($"  - BankAccounts: {bankAccountsUpdated}");
                logMessages.Add($"  - BankAccountBalances: {bankAccountBalancesUpdated}");

                // Log admin activity
                _context.AdminActivities.Add(new AdminActivity
                {
                    AdminUserId = user?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.BulkOperation,
                    Description = $"Populated CurrencyId from CurrencyCode: {totalUpdated} records updated",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                await _context.SaveChangesAsync();

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Migration completed successfully. {totalUpdated} records updated.",
                        details = logMessages
                    });
                }

                TempData["Success"] = string.Join("<br/>", logMessages);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در migration: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در migration: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

    }
}
