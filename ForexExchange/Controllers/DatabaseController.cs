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
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO;
using System.Text;
using ForexExchange.Authorization;

namespace ForexExchange.Controllers
{
     [HasPermission(Permissions.Database_Management)]
    public class DatabaseController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICurrencyPoolService _currencyPoolService;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly INotificationHub _notificationHub;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICsvImportService _csvImportService;
        private readonly IOrderDataService _orderDataService;
        private readonly ILogger<DatabaseController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public DatabaseController(ForexDbContext context, IWebHostEnvironment environment,
            ICurrencyPoolService currencyPoolService, ICentralFinancialService centralFinancialService,
            INotificationHub notificationHub, UserManager<ApplicationUser> userManager,
            ICsvImportService csvImportService, IOrderDataService orderDataService,
            ILogger<DatabaseController> logger, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _environment = environment;
            _currencyPoolService = currencyPoolService;
            _centralFinancialService = centralFinancialService;
            _notificationHub = notificationHub;
            _userManager = userManager;
            _csvImportService = csvImportService;
            _orderDataService = orderDataService;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DatabaseManagementViewModel
            {
                CustomersCount = _context.Customers.Where(c => !c.IsSystem).Count(),
                OrdersCount = _context.Orders.Count(),
                CurrencyPoolsCount = _context.CurrencyPools.Count(),
                TransactionsCount = 0,
                ExchangeRatesCount = _context.ExchangeRates.Count(),
                AccountingDocumentsCount = _context.AccountingDocuments.Count()
            };
            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName)
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.FullName ?? c.Id.ToString() })
                .ToListAsync();

            var logsDir = Path.Combine(_environment.ContentRootPath, "Logs");
            var runningPath = Path.Combine(logsDir, "MatchDocsRunning.txt");
            var logPath = Path.Combine(logsDir, "MatchDocsLog.txt");
            if (System.IO.File.Exists(runningPath))
                ViewBag.MatchDocsRunning = true;
            if (System.IO.File.Exists(logPath))
            {
                try { ViewBag.MatchDocsLog = await System.IO.File.ReadAllTextAsync(logPath, Encoding.UTF8); } catch { }
            }

            return View(model);
        }

        [HttpGet]
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
        public async Task<IActionResult> CreateBackup()
        {
            string? tempBackupPath = null;
            try
            {
                var now = DateTime.Now;
                var backupFileName = $"Taban_Backup_{now.GetPersianYear()}-{now.GetPersianMonth()}-{now.GetPersianDayOfMonth()}-{now.Hour}-{now.Minute}.db";
                var backupPath = Path.Combine(_environment.WebRootPath, "backups");

                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                var fullBackupPath = Path.Combine(backupPath, backupFileName);

                // Get database connection string and path
                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string is not configured");
                }

                var dbPath = connectionString?.Replace("Data Source=", "").Split(';')[0].Trim();
                if (string.IsNullOrEmpty(dbPath) || !System.IO.File.Exists(dbPath))
                {
                    throw new FileNotFoundException($"Database file not found: {dbPath}");
                }

                // Backup to a TEMPORARY file first so SQLite never holds the final backup path.
                // On Windows, SQLite can keep the file handle briefly after Dispose; writing to
                // temp and then copying avoids "file is being used by another process" when reading.
                tempBackupPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
                var sourceConnectionString = $"Data Source={dbPath};Cache=Shared";
                var backupConnectionString = $"Data Source={tempBackupPath};Cache=Shared";

                using (var sourceConnection = new SqliteConnection(sourceConnectionString))
                using (var backupConnection = new SqliteConnection(backupConnectionString))
                {
                    await sourceConnection.OpenAsync();
                    await backupConnection.OpenAsync();

                    using (var cmd = sourceConnection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=10000;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = backupConnection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=10000;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = sourceConnection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    sourceConnection.BackupDatabase(backupConnection);
                }

                // Copy temp backup to final path with retries (Windows may release file handle shortly after Dispose)
                const int maxRetries = 5;
                const int delayMs = 150;
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        System.IO.File.Copy(tempBackupPath, fullBackupPath, overwrite: true);
                        break;
                    }
                    catch (IOException) when (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                    }
                }

                // Verify backup file was created and has content
                if (!System.IO.File.Exists(fullBackupPath))
                {
                    throw new InvalidOperationException("Backup file was not created");
                }

                var fileInfo = new FileInfo(fullBackupPath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Backup file is empty");
                }

                // Read from final path (never held by SQLite) and return for download
                var fileBytes = await System.IO.File.ReadAllBytesAsync(fullBackupPath);
                return File(fileBytes, "application/octet-stream", backupFileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در ایجاد پشتیبان: {ex.Message}";
                return Json(new { success = false, error = ex.Message });
            }
            finally
            {
                // Clean up temp file if it exists
                if (tempBackupPath != null && System.IO.File.Exists(tempBackupPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempBackupPath);
                    }
                    catch
                    {
                        // Ignore; temp will be cleared by OS later
                    }
                }
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

            // Validate file extension
            var allowedExtensions = new[] { ".db", ".tbn", ".sqlite", ".sqlite3" };
            var fileExtension = Path.GetExtension(backupFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["Error"] = $"فرمت فایل نامعتبر است. فرمت‌های مجاز: {string.Join(", ", allowedExtensions)}";
                return RedirectToAction("Index");
            }

            try
            {
                // Get database connection string and path
                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string is not configured");
                }

                var dbPath = connectionString?.Replace("Data Source=", "").Split(';')[0].Trim();
                if (string.IsNullOrEmpty(dbPath) || !System.IO.File.Exists(dbPath))
                {
                    throw new FileNotFoundException($"Database file not found: {dbPath}");
                }

                // Create automatic backup before restore using SQLite Backup API (includes WAL)
                var now = DateTime.Now;
                var backupFileName = $"-Auto-Taban_Backup_{now.GetPersianYear()}-{now.GetPersianMonth()}-{now.GetPersianDayOfMonth()}-{now.Hour}-{now.Minute}.db";
                var backupPath = Path.Combine(_environment.WebRootPath, "backups");
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                var autoBackupPath = Path.Combine(backupPath, backupFileName);

                // Create automatic backup using SQLite Backup API to ensure all data (including WAL) is backed up
                var sourceConnectionString = $"Data Source={dbPath};Cache=Shared";
                var autoBackupConnectionString = $"Data Source={autoBackupPath};Cache=Shared";

                using (var sourceConnection = new SqliteConnection(sourceConnectionString))
                using (var autoBackupConnection = new SqliteConnection(autoBackupConnectionString))
                {
                    await sourceConnection.OpenAsync();
                    await autoBackupConnection.OpenAsync();

                    // Set busy timeout
                    using (var cmd = sourceConnection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=10000;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = autoBackupConnection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=10000;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Perform a full checkpoint to ensure all WAL data is written to main database
                    using (var cmd = sourceConnection.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Backup current database before restore
                    sourceConnection.BackupDatabase(autoBackupConnection);
                }

                // Verify automatic backup was created
                if (!System.IO.File.Exists(autoBackupPath))
                {
                    throw new InvalidOperationException("Automatic backup file was not created");
                }

                // Save uploaded file temporarily
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await backupFile.CopyToAsync(stream);
                }

                // Verify uploaded file is a valid SQLite database
                try
                {
                    var testConnectionString = $"Data Source={tempPath};Mode=ReadOnly;Cache=Shared";
                    using (var testConnection = new SqliteConnection(testConnectionString))
                    {
                        await testConnection.OpenAsync();
                        using (var cmd = testConnection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table';";
                            var tableCount = await cmd.ExecuteScalarAsync();
                            if (tableCount == null || Convert.ToInt32(tableCount) == 0)
                            {
                                throw new InvalidOperationException("فایل آپلود شده یک دیتابیس SQLite معتبر نیست یا خالی است");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Cleanup temp file
                    try { System.IO.File.Delete(tempPath); } catch { }
                    throw new InvalidOperationException($"فایل آپلود شده معتبر نیست: {ex.Message}");
                }

                // Use SQLite backup API to copy contents from uploaded DB into the live database
                // This avoids deleting/replacing the file while it's in use
                var busyDest = $"Data Source={dbPath};Cache=Shared";
                var busySrc = $"Data Source={tempPath};Mode=ReadOnly;Cache=Shared";

                using (var dest = new SqliteConnection(busyDest))
                using (var src = new SqliteConnection(busySrc))
                {
                    await dest.OpenAsync();
                    await src.OpenAsync();

                    // Set busy timeout via PRAGMA on both connections
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=10000;";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    using (var cmd = src.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=10000;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Ensure WAL is checkpointed to reduce locks
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Temporarily disable foreign keys during restore to avoid temporary constraint errors
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA foreign_keys=OFF;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Copy database content from uploaded file to live database
                    src.BackupDatabase(dest);

                    // Re-enable foreign keys
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA foreign_keys=ON;";
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Perform a final checkpoint to ensure all data is written
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        await cmd.ExecuteNonQueryAsync();
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
        public async Task<IActionResult> FreezeAllOrders(DateTime? freezeBefore)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";

                var ordersFrozen = await _centralFinancialService.FreezeAllOrdersAsync(performedBy, freezeBefore);

                var freezeTimestamp = DateTime.UtcNow;

                var manualPoolSoftDeleted = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted &&
                    h.TransactionDate <= freezeBefore)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(h => h.IsDeleted, _ => true)
                        .SetProperty(h => h.DeletedAt, _ => freezeTimestamp)
                        .SetProperty(h => h.DeletedBy, _ => performedBy));

                await _centralFinancialService.RebuildAllFinancialBalancesAsync(performedBy);
                await _currencyPoolService.UpdateAllOrderCountsAsync();
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

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> FixMan()
        {
            try
            {
                var cutoffDate = new DateTime(2026, 1, 1);

                var manualPoolSoftDeleted = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && h.IsDeleted &&
                    h.TransactionDate >= cutoffDate)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(h => h.IsDeleted, _ => false)
                        .SetProperty(h => h.DeletedAt, _ => null)
                        .SetProperty(h => h.DeletedBy, _ => null));

                var successMessage = $" manual adjusments restored succesfully   .<br/>";

                return Json(new
                {
                    success = true,
                    message = successMessage,
                    manualPoolSoftDeleted
                });
            }
            catch (Exception ex)
            {
                var errorMessage = $"  manul adjustment داشبورد  : {ex.Message}";

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
        /// حذف اسناد حسابداری تکراری: معیار = همان ReferenceNumber، مبلغ، پرداخت‌کننده و دریافت‌کننده.
        /// در هر گروه تکراری اولین سند (کمترین Id) نگه داشته می‌شود و بقیه با برگرداندن تأثیر مالی حذف می‌شوند.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDuplicateAccountingDocuments()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";
                var logMessages = new List<string>();

                logMessages.Add("=== حذف اسناد حسابداری تکراری ===");
                logMessages.Add($"شروع: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add($"اجرا شده توسط: {performedBy}");
                logMessages.Add("معیار تکراری: همان ReferenceNumber، Amount، Payer (مشتری/حساب)، Receiver (مشتری/حساب)");
                logMessages.Add("");

                var docs = await _context.AccountingDocuments
                    .OrderBy(d => d.Id)
                    .ToListAsync();

                var duplicateGroups = docs
                    .GroupBy(d => new
                    {
                        Ref = (d.ReferenceNumber ?? "").Trim(),
                        d.Amount,
                        PayerCustomerId = d.PayerCustomerId ?? -1,
                        PayerBankAccountId = d.PayerBankAccountId ?? -1,
                        ReceiverCustomerId = d.ReceiverCustomerId ?? -1,
                        ReceiverBankAccountId = d.ReceiverBankAccountId ?? -1
                    })
                    .Where(g => g.Count() > 1)
                    .ToList();

                int deletedCount = 0;
                foreach (var group in duplicateGroups)
                {
                    var toDelete = group.OrderBy(d => d.Id).Skip(1).ToList();
                    foreach (var doc in toDelete)
                    {
                        try
                        {
                            await _centralFinancialService.DeleteAccountingDocumentAsync(doc, performedBy);
                            deletedCount++;
                            logMessages.Add($"  حذف سند تکراری #{doc.Id} (Ref: {doc.ReferenceNumber}, مبلغ: {doc.Amount})");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting duplicate document {DocId}", doc.Id);
                            logMessages.Add($"  خطا در حذف سند #{doc.Id}: {ex.Message}");
                        }
                    }
                }

                if (deletedCount == 0)
                    logMessages.Add("هیچ سند تکراری یافت نشد.");
                else
                    logMessages.Add($"مجموع: {deletedCount} سند تکراری حذف شد.");

                logMessages.Add("");
                logMessages.Add("=== عملیات انجام شد ===");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        message = deletedCount > 0
                            ? $"حذف اسناد تکراری با موفقیت انجام شد. {deletedCount} سند حذف شد."
                            : "هیچ سند تکراری یافت نشد.",
                        deletedCount,
                        logLines = logMessages
                    });
                }

                TempData["Success"] = string.Join("<br/>", logMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RemoveDuplicateAccountingDocuments failed");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, error = $"خطا: {ex.Message}" });
                TempData["Error"] = $"خطا در حذف اسناد تکراری: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
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

        /// <summary>
        /// Upload unified CSV (documents + orders) for one customer. Documents are two-sided: new ref = create with temp bank; existing ref = update other side if temp, else mark duplicate for review.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(10_485_760)]
        public async Task<IActionResult> UploadCustomerCsv(IFormFile csvFile, int customerId)
        {
            var importedDocs = 0;
            var updatedDocs = 0;
            var skippedDocs = 0;
            var importedOrders = 0;
            var skippedOrders = 0;
            var duplicateRefs = new List<string>();
            var duplicateForReview = new List<object>();
            var errors = new List<object>();

            if (csvFile == null || csvFile.Length == 0)
            {
                return Json(new { success = false, message = "فایلی انتخاب نشده است.", importedDocs = 0, updatedDocs = 0, skippedDocs = 0, importedOrders = 0, skippedOrders = 0, duplicateRefs, duplicateForReview, errors });
            }

            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return Json(new { success = false, message = "مشتری یافت نشد.", importedDocs = 0, updatedDocs = 0, skippedDocs = 0, importedOrders = 0, skippedOrders = 0, duplicateRefs, duplicateForReview, errors });
                }

                UnifiedCsvParseResult parseResult;
                try
                {
                    using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
                    parseResult = ParseUnifiedCustomerCsv(reader, errors);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing unified customer CSV");
                    return Json(new { success = false, message = $"خطا در خواندن فایل: {ex.Message}", importedDocs = 0, updatedDocs = 0, skippedDocs = 0, importedOrders = 0, skippedOrders = 0, duplicateRefs, duplicateForReview, errors });
                }

                var currencies = await _context.Currencies.Where(c => c.Code != null).ToListAsync();

                // --- Documents (Receive / Pay) - two-sided logic ---
                // همه اسناد با این رفرنس‌ها را می‌گیریم تا هم داپلیکیت را با (مشتری+رفرنس+مبلغ+طرف) چک کنیم هم در صورت وجود طرف موقت، به‌روزرسانی کنیم
                var docRefs = parseResult.DocRows.Select(r => r.ReferenceNumber).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                var existingDocsAll = docRefs.Count > 0
                    ? await _context.AccountingDocuments
                        .Where(a => a.ReferenceNumber != null && docRefs.Contains(a.ReferenceNumber))
                        .ToListAsync()
                    : new List<AccountingDocument>();

                foreach (var row in parseResult.DocRows)
                {
                    var refId = row.ReferenceNumber ?? "";
                    if (string.IsNullOrWhiteSpace(refId)) continue;

                    var codeUpper = (row.CurrencyCode ?? "").Trim().ToUpperInvariant();
                    var currency = currencies.FirstOrDefault(c => (c.Code ?? "").Trim().ToUpperInvariant() == codeUpper);
                    if (currency == null)
                    {
                        errors.Add(new { refId, message = "ارز معتبر نیست." });
                        continue;
                    }

                    var amount = Math.Abs(row.Amount);
                    if (amount <= 0) continue;

                    BankAccount tempBank;
                    try
                    {
                        tempBank = await _csvImportService.GetOrCreateTempBankAccountForCsvImportAsync(currency.Id, currency.Code ?? "IRR");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to get/create temp bank for CSV import");
                        errors.Add(new { refId, message = ex.Message });
                        continue;
                    }

                    var isReceive = row.Type?.Trim().Equals("Receive", StringComparison.OrdinalIgnoreCase) == true;

                    // داپلیکیت واقعی: همین مشتری با همین رفرنس و همان مبلغ در همان نقش (پرداخت‌کننده یا دریافت‌کننده) از قبل وجود دارد
                    var isDuplicate = existingDocsAll.Any(d =>
                        (d.ReferenceNumber ?? "").Trim() == refId
                        && Math.Abs(d.Amount - amount) < 0.01m
                        && (isReceive ? d.ReceiverCustomerId == customerId : d.PayerCustomerId == customerId));
                    if (isDuplicate)
                    {
                        skippedDocs++;
                        duplicateRefs.Add(refId);
                        duplicateForReview.Add(new { refId, reason = "این سند (همان مشتری و طرف) از قبل وجود دارد." });
                        continue;
                    }

                    // آیا سندی با این رفرنس و مبلغ وجود دارد که یک طرفش موقت باشد تا آن را با این مشتری پر کنیم؟
                    var existingDoc = existingDocsAll.FirstOrDefault(d =>
                        (d.ReferenceNumber ?? "").Trim() == refId
                        && Math.Abs(d.Amount - amount) < 0.01m
                        && ((d.PayerType == PayerType.System && d.PayerBankAccountId == tempBank.Id)
                            || (d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId == tempBank.Id)));
                    var payerIsTemp = existingDoc != null && existingDoc.PayerType == PayerType.System && existingDoc.PayerBankAccountId == tempBank.Id;
                    var receiverIsTemp = existingDoc != null && existingDoc.ReceiverType == ReceiverType.System && existingDoc.ReceiverBankAccountId == tempBank.Id;

                    if (existingDoc != null && ((!isReceive && payerIsTemp) || (isReceive && receiverIsTemp)))
                    {
                        // پر کردن طرف موقت با این مشتری
                        if (!isReceive && payerIsTemp)
                        {
                            existingDoc.PayerType = PayerType.Customer;
                            existingDoc.PayerCustomerId = customerId;
                            existingDoc.PayerBankAccountId = null;
                        }
                        else
                        {
                            existingDoc.ReceiverType = ReceiverType.Customer;
                            existingDoc.ReceiverCustomerId = customerId;
                            existingDoc.ReceiverBankAccountId = null;
                        }
                        try
                        {
                            _context.Update(existingDoc);
                            await _context.SaveChangesAsync();
                            updatedDocs++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating document other side for ref {RefId}", refId);
                            errors.Add(new { refId, message = ex.Message });
                        }
                        continue;
                    }

                    // سند جدید: مشتری + بانک موقت
                    var doc = new AccountingDocument
                    {
                        Type = DocumentType.Havala,
                        CurrencyId = currency.Id,
                        CurrencyCode = currency.Code ?? "IRR",
                        Amount = amount,
                        Title = $"ورود CSV - {refId}",
                        Description = row.Note,
                        DocumentDate = row.Date ?? DateTime.Today,
                        CreatedAt = DateTime.Now,
                        ReferenceNumber = refId,
                        IsVerified = true,
                        IsFrozen = false
                    };
                    if (isReceive)
                    {
                        doc.PayerType = PayerType.System;
                        doc.PayerBankAccountId = tempBank.Id;
                        doc.ReceiverType = ReceiverType.Customer;
                        doc.ReceiverCustomerId = customerId;
                    }
                    else
                    {
                        doc.PayerType = PayerType.Customer;
                        doc.PayerCustomerId = customerId;
                        doc.ReceiverType = ReceiverType.System;
                        doc.ReceiverBankAccountId = tempBank.Id;
                    }
                    try
                    {
                        _context.Add(doc);
                        await _context.SaveChangesAsync();
                        importedDocs++;
                        existingDocsAll.Add(doc);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving document for ref {RefId}", refId);
                        errors.Add(new { refId, message = ex.Message });
                    }
                }

                // --- Orders (Buy + Sell by referenceNumber) ---
                var orderExistingRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var ordersWithImportRef = await _context.Orders
                    .Where(o => o.CustomerId == customerId && o.Notes != null && o.Notes.Contains("ImportRef:"))
                    .Select(o => o.Notes)
                    .ToListAsync();
                foreach (var notes in ordersWithImportRef)
                {
                    if (notes == null) continue;
                    var idx = notes.IndexOf("ImportRef:", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    var start = idx + "ImportRef:".Length;
                    var end = notes.IndexOf(' ', start);
                    if (end < 0) end = notes.Length;
                    var refId = notes.Substring(start, Math.Min(end - start, notes.Length - start)).Trim();
                    if (!string.IsNullOrEmpty(refId)) orderExistingRefs.Add(refId);
                }

                var orderGroups = parseResult.OrderRows.GroupBy(r => r.ReferenceNumber ?? "").Where(g => !string.IsNullOrWhiteSpace(g.Key)).ToList();

                foreach (var group in orderGroups)
                {
                    var refId = group.Key;
                    if (orderExistingRefs.Contains(refId))
                    {
                        skippedOrders++;
                        duplicateRefs.Add(refId);
                        continue;
                    }

                    // Sell = FromCurrency / FromAmount ; Buy = ToCurrency / ToAmount (یک رفرنس = یک معامله)
                    var list = group.ToList();
                    var sellRow = list.FirstOrDefault(r => r.Type?.Equals("Sell", StringComparison.OrdinalIgnoreCase) == true);
                    var buyRow = list.FirstOrDefault(r => r.Type?.Equals("Buy", StringComparison.OrdinalIgnoreCase) == true);

                    if (buyRow == null || sellRow == null)
                    {
                        errors.Add(new { refId, message = "برای هر سفارش باید یک ردیف Buy و یک ردیف Sell با همان referenceNumber وجود داشته باشد." });
                        continue;
                    }

                    var fromCode = sellRow.CurrencyCode?.Trim();
                    var toCode = buyRow.CurrencyCode?.Trim();
                    var fromCurrency = currencies.FirstOrDefault(c => (c.Code ?? "").Trim().ToUpperInvariant() == (fromCode ?? "").ToUpperInvariant());
                    var toCurrency = currencies.FirstOrDefault(c => (c.Code ?? "").Trim().ToUpperInvariant() == (toCode ?? "").ToUpperInvariant());
                    if (fromCurrency == null || toCurrency == null)
                    {
                        errors.Add(new { refId, message = "ارز مبدا یا مقصد معتبر نیست." });
                        continue;
                    }

                    var fromAmount = Math.Abs(sellRow.Amount);
                    var toAmount = Math.Abs(buyRow.Amount);
                    if (fromAmount <= 0 || toAmount <= 0)
                    {
                        errors.Add(new { refId, message = "مبلغ باید بزرگتر از صفر باشد." });
                        continue;
                    }

                    var rate = toAmount / fromAmount;
                    var createdAt = sellRow.Date ?? buyRow.Date ?? DateTime.Today;
                    var notes = (sellRow.Note ?? buyRow.Note ?? "") + " ImportRef:" + refId;

                    var dto = new OrderFormDataDto
                    {
                        CustomerId = customerId,
                        FromCurrencyId = fromCurrency.Id,
                        ToCurrencyId = toCurrency.Id,
                        FromAmount = fromAmount,
                        ToAmount = toAmount,
                        Rate = rate,
                        CreatedAt = createdAt,
                        Notes = notes
                    };

                    try
                    {
                        var orderResult = await _orderDataService.PrepareOrderFromFormDataAsync(dto);
                        if (!orderResult.IsSuccess)
                        {
                            errors.Add(new { refId, message = orderResult.ErrorMessage ?? "خطا در آماده‌سازی سفارش." });
                            continue;
                        }
                        var order = orderResult.Order!;
                        order.Notes = notes;
                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();
                        importedOrders++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating order for ref {RefId}", refId);
                        errors.Add(new { refId, message = ex.Message });
                    }
                }

                var message = $"واردات انجام شد. اسناد: {importedDocs} جدید، {updatedDocs} به‌روز (طرف دوم)، {skippedDocs} رد شده؛ سفارش‌ها: {importedOrders} وارد شده، {skippedOrders} رد شده.";
                return Json(new
                {
                    success = true,
                    message,
                    importedDocs,
                    updatedDocs,
                    skippedDocs,
                    importedOrders,
                    skippedOrders,
                    duplicateRefs,
                    duplicateForReview,
                    errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadCustomerCsv");
                return Json(new { success = false, message = $"خطا در ارتباط با سرور: {ex.Message}", importedDocs = 0, updatedDocs = 0, skippedDocs = 0, importedOrders = 0, skippedOrders = 0, duplicateRefs = new List<string>(), duplicateForReview = new List<object>(), errors = new List<object>() });
            }
        }

        private static UnifiedCsvParseResult ParseUnifiedCustomerCsv(StreamReader reader, List<object> errors)
        {
            var docRows = new List<UnifiedDocRow>();
            var orderRows = new List<UnifiedOrderRow>();
            var lineNum = 0;
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine)) return new UnifiedCsvParseResult { DocRows = docRows, OrderRows = orderRows };
            lineNum++;
            // Strip BOM if present (e.g. UTF-8 BOM makes first header "﻿date")
            var headerLineNormalized = headerLine!.TrimStart('\uFEFF');
            var headers = headerLineNormalized.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            int idxDate = Array.FindIndex(headers, h => h == "date");
            int idxType = Array.FindIndex(headers, h => h == "type");
            int idxCurrency = Array.FindIndex(headers, h => h == "currency");
            // Accept "referencenumber", "refrencenumber" (typo), "reference_number", "transactionid", "id"
            int idxRef = Array.FindIndex(headers, h => h == "referencenumber" || h == "refrencenumber" || (h.Contains("reference") && h.Contains("number")) || h == "reference_number");
            if (idxRef < 0) idxRef = Array.FindIndex(headers, h => h == "transactionid" || h == "id");
            int idxAmount = Array.FindIndex(headers, h => h == "amount");
            int idxNote = Array.FindIndex(headers, h => h == "note");
            int idxDesc = Array.FindIndex(headers, h => h == "description");
            if (idxNote < 0 && idxDesc >= 0) idxNote = idxDesc;

            if (idxDate < 0 || idxType < 0 || idxRef < 0 || idxAmount < 0)
            {
                errors.Add(new { line = lineNum, message = "ستون‌های ضروری (date, type, referenceNumber, amount) یافت نشد." });
                return new UnifiedCsvParseResult { DocRows = docRows, OrderRows = orderRows };
            }

            while (reader.ReadLine() is { } line)
            {
                lineNum++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = SplitCsvLine(line);
                var maxIdx = Math.Max(Math.Max(idxDate, idxType), Math.Max(idxRef, idxAmount));
                if (parts.Count <= maxIdx) continue;

                var refId = idxRef < parts.Count ? NormalizeRefId(parts[idxRef]) : "";
                var type = (idxType >= 0 && idxType < parts.Count) ? parts[idxType]?.Trim() : null;
                var isDocRow = type?.Equals("Receive", StringComparison.OrdinalIgnoreCase) == true || type?.Equals("Pay", StringComparison.OrdinalIgnoreCase) == true;
                if (string.IsNullOrWhiteSpace(refId) || refId == "-")
                {
                    if (isDocRow)
                        refId = "IMPORT-" + lineNum + "-" + Guid.NewGuid().ToString("N")[..8];
                    else
                        continue; // Orders need ref to match Buy+Sell
                }

                DateTime? date = null;
                if (idxDate >= 0 && idxDate < parts.Count && DateTime.TryParse(parts[idxDate], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    date = d;
                var currency = (idxCurrency >= 0 && idxCurrency < parts.Count) ? parts[idxCurrency]?.Trim() : null;
                var amountStr = (idxAmount >= 0 && idxAmount < parts.Count) ? parts[idxAmount].Replace(",", "", StringComparison.Ordinal).Trim() : "0";
                if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt)) continue;
                var note = (idxNote >= 0 && idxNote < parts.Count) ? parts[idxNote] : null;

                if (type?.Equals("Receive", StringComparison.OrdinalIgnoreCase) == true || type?.Equals("Pay", StringComparison.OrdinalIgnoreCase) == true)
                {
                    docRows.Add(new UnifiedDocRow { ReferenceNumber = refId, Date = date, Type = type, CurrencyCode = currency, Amount = amt, Note = note });
                }
                else if (type?.Equals("Buy", StringComparison.OrdinalIgnoreCase) == true || type?.Equals("Sell", StringComparison.OrdinalIgnoreCase) == true)
                {
                    orderRows.Add(new UnifiedOrderRow { ReferenceNumber = refId, Date = date, Type = type, CurrencyCode = currency, Amount = amt, Note = note });
                }
            }
            return new UnifiedCsvParseResult { DocRows = docRows, OrderRows = orderRows };
        }

        private static List<string> SplitCsvLine(string line)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (!inQuotes && c == ',') { list.Add(sb.ToString().Trim()); sb.Clear(); continue; }
                sb.Append(c);
            }
            list.Add(sb.ToString().Trim());
            return list;
        }

        private static string NormalizeRefId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private class UnifiedCsvParseResult
        {
            public List<UnifiedDocRow> DocRows { get; set; } = new();
            public List<UnifiedOrderRow> OrderRows { get; set; } = new();
        }

        private sealed class UnifiedDocRow
        {
            public string? ReferenceNumber { get; set; }
            public DateTime? Date { get; set; }
            public string? Type { get; set; }
            public string? CurrencyCode { get; set; }
            public decimal Amount { get; set; }
            public string? Note { get; set; }
        }

        private sealed class UnifiedOrderRow
        {
            public string? ReferenceNumber { get; set; }
            public DateTime? Date { get; set; }
            public string? Type { get; set; }
            public string? CurrencyCode { get; set; }
            public decimal Amount { get; set; }
            public string? Note { get; set; }
        }

        /// <summary>
        /// Starts document matching in background to avoid timeout. Log is written to Logs/MatchDocsLog.txt; refresh page to see result.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MatchAccountingDocuments()
        {
            var logsDir = Path.Combine(_environment.ContentRootPath, "Logs");
            var runningPath = Path.Combine(logsDir, "MatchDocsRunning.txt");
            var logPath = Path.Combine(logsDir, "MatchDocsLog.txt");
            try
            {
                Directory.CreateDirectory(logsDir);
                System.IO.File.WriteAllText(runningPath, DateTime.Now.ToString("O"));
                var scopeFactory = _scopeFactory;
                var env = _environment;
                var log = _logger;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var ctx = scope.ServiceProvider.GetRequiredService<ForexDbContext>();
                        var logLines = await RunMatchAccountingDocumentsCoreAsync(ctx);
                        var outPath = Path.Combine(env.ContentRootPath, "Logs", "MatchDocsLog.txt");
                        await System.IO.File.WriteAllTextAsync(outPath, string.Join("\n", logLines), Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "MatchAccountingDocuments background failed");
                        var outPath = Path.Combine(env.ContentRootPath, "Logs", "MatchDocsLog.txt");
                        await System.IO.File.WriteAllTextAsync(outPath, "خطا: " + ex.Message + "\n" + ex.StackTrace, Encoding.UTF8);
                    }
                    finally
                    {
                        try { System.IO.File.Delete(Path.Combine(env.ContentRootPath, "Logs", "MatchDocsRunning.txt")); } catch { }
                    }
                });
                TempData["Success"] = "عملیات تطبیق اسناد در پس‌زمینه شروع شد. پس از چند دقیقه صفحه را رفرش کنید تا لاگ نمایش داده شود.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MatchAccountingDocuments start failed");
                try { if (System.IO.File.Exists(runningPath)) System.IO.File.Delete(runningPath); } catch { }
                TempData["Error"] = "خطا در شروع تطبیق اسناد: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        private static async Task<List<string>> RunMatchAccountingDocumentsCoreAsync(ForexDbContext ctx)
        {
            var log = new List<string>();
            var tempBankIds = await ctx.BankAccounts
                .Where(b => b.AccountNumber != null && b.AccountNumber.StartsWith("IMPORT-TEMP-"))
                .Select(b => b.Id)
                .ToListAsync();

            var docs = await ctx.AccountingDocuments
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.ReferenceNumber)
                .ThenBy(a => a.Id)
                .ToListAsync();

            var withRef = docs.Where(d => !string.IsNullOrWhiteSpace(d.ReferenceNumber)).ToList();
            var byRef = withRef.GroupBy(d => (d.ReferenceNumber ?? "").Trim()).Where(g => !string.IsNullOrWhiteSpace(g.Key)).ToList();

            int matchedByRef = 0;
            foreach (var group in byRef)
            {
                var list = group.ToList();
                if (list.Count != 2) { log.Add($"Ref {group.Key}: تعداد اسناد {list.Count} (انتظار 2)."); continue; }

                var doc1 = list[0];
                var doc2 = list[1];

                var d1PayerTemp = doc1.PayerType == PayerType.System && doc1.PayerBankAccountId.HasValue && tempBankIds.Contains(doc1.PayerBankAccountId.Value);
                var d1ReceiverTemp = doc1.ReceiverType == ReceiverType.System && doc1.ReceiverBankAccountId.HasValue && tempBankIds.Contains(doc1.ReceiverBankAccountId.Value);
                var d2PayerTemp = doc2.PayerType == PayerType.System && doc2.PayerBankAccountId.HasValue && tempBankIds.Contains(doc2.PayerBankAccountId.Value);
                var d2ReceiverTemp = doc2.ReceiverType == ReceiverType.System && doc2.ReceiverBankAccountId.HasValue && tempBankIds.Contains(doc2.ReceiverBankAccountId.Value);

                if (d1PayerTemp && doc1.ReceiverCustomerId.HasValue && d2ReceiverTemp && doc2.PayerCustomerId.HasValue)
                {
                    doc1.PayerType = PayerType.Customer;
                    doc1.PayerCustomerId = doc2.PayerCustomerId;
                    doc1.PayerBankAccountId = null;
                    doc2.ReceiverType = ReceiverType.Customer;
                    doc2.ReceiverCustomerId = doc1.ReceiverCustomerId;
                    doc2.ReceiverBankAccountId = null;
                    matchedByRef++;
                    log.Add($"Ref {group.Key}: سند {doc1.Id} و {doc2.Id} به‌روز شد (طرف موقت با مشتری جایگزین شد).");
                }
                else if (d1ReceiverTemp && doc1.PayerCustomerId.HasValue && d2PayerTemp && doc2.ReceiverCustomerId.HasValue)
                {
                    doc1.ReceiverType = ReceiverType.Customer;
                    doc1.ReceiverCustomerId = doc2.ReceiverCustomerId;
                    doc1.ReceiverBankAccountId = null;
                    doc2.PayerType = PayerType.Customer;
                    doc2.PayerCustomerId = doc1.PayerCustomerId;
                    doc2.PayerBankAccountId = null;
                    matchedByRef++;
                    log.Add($"Ref {group.Key}: سند {doc1.Id} و {doc2.Id} به‌روز شد.");
                }
            }

            var noRef = docs.Where(d => string.IsNullOrWhiteSpace(d.ReferenceNumber)).ToList();
            int matchedByParams = 0;
            var used = new HashSet<int>();

            foreach (var doc1 in noRef)
            {
                if (used.Contains(doc1.Id)) continue;
                var d1PayerTemp = doc1.PayerType == PayerType.System && doc1.PayerBankAccountId.HasValue && tempBankIds.Contains(doc1.PayerBankAccountId.Value);
                var d1ReceiverTemp = doc1.ReceiverType == ReceiverType.System && doc1.ReceiverBankAccountId.HasValue && tempBankIds.Contains(doc1.ReceiverBankAccountId.Value);
                if (!d1PayerTemp && !d1ReceiverTemp) continue;

                var match = noRef.FirstOrDefault(d2 => d2.Id != doc1.Id && !used.Contains(d2.Id)
                    && d2.CurrencyId == doc1.CurrencyId
                    && Math.Abs(d2.Amount - doc1.Amount) < 0.01m
                    && d2.DocumentDate.Date == doc1.DocumentDate.Date
                    && ((d1PayerTemp && doc1.ReceiverCustomerId.HasValue && d2.ReceiverType == ReceiverType.System && d2.ReceiverBankAccountId.HasValue && tempBankIds.Contains(d2.ReceiverBankAccountId.Value) && d2.PayerCustomerId.HasValue)
                      || (d1ReceiverTemp && doc1.PayerCustomerId.HasValue && d2.PayerType == PayerType.System && d2.PayerBankAccountId.HasValue && tempBankIds.Contains(d2.PayerBankAccountId.Value) && d2.ReceiverCustomerId.HasValue)));

                if (match == null) continue;

                if (d1PayerTemp && match.PayerCustomerId.HasValue)
                {
                    doc1.PayerType = PayerType.Customer;
                    doc1.PayerCustomerId = match.PayerCustomerId;
                    doc1.PayerBankAccountId = null;
                    match.ReceiverType = ReceiverType.Customer;
                    match.ReceiverCustomerId = doc1.ReceiverCustomerId;
                    match.ReceiverBankAccountId = null;
                }
                else
                {
                    doc1.ReceiverType = ReceiverType.Customer;
                    doc1.ReceiverCustomerId = match.ReceiverCustomerId;
                    doc1.ReceiverBankAccountId = null;
                    match.PayerType = PayerType.Customer;
                    match.PayerCustomerId = doc1.PayerCustomerId;
                    match.PayerBankAccountId = null;
                }
                matchedByParams++;
                used.Add(doc1.Id);
                used.Add(match.Id);
                log.Add($"بدون رفرنس: سند {doc1.Id} و {match.Id} با مبلغ/ارز/تاریخ یکسان به‌روز شد.");
            }

            await ctx.SaveChangesAsync();

            log.Insert(0, $"تطبیق اسناد: {matchedByRef} جفت با ReferenceNumber، {matchedByParams} جفت بدون رفرنس (بر اساس مبلغ/ارز/تاریخ).");
            return log;
        }

        /// <summary>
        /// Scans Orders, finds groups with same ImportRef (in Notes) per customer, reports duplicates for review.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MatchOrders()
        {
            var log = new List<string>();
            try
            {
                var orders = await _context.Orders
                    .Where(o => !o.IsDeleted && o.Notes != null && o.Notes.Contains("ImportRef:"))
                    .Select(o => new { o.Id, o.CustomerId, o.Notes, o.FromAmount, o.ToAmount, o.CreatedAt })
                    .ToListAsync();

                var refs = new Dictionary<string, List<(int OrderId, int CustomerId)>>();
                foreach (var o in orders)
                {
                    var idx = o.Notes!.IndexOf("ImportRef:", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    var start = idx + "ImportRef:".Length;
                    var end = o.Notes.IndexOf(' ', start);
                    if (end < 0) end = o.Notes.Length;
                    var refId = o.Notes.Substring(start, Math.Min(end - start, o.Notes.Length - start)).Trim();
                    if (string.IsNullOrEmpty(refId)) continue;
                    var key = refId + "@" + o.CustomerId;
                    if (!refs.ContainsKey(key)) refs[key] = new List<(int, int)>();
                    refs[key].Add((o.Id, o.CustomerId));
                }

                int duplicateGroups = 0;
                foreach (var kv in refs.Where(x => x.Value.Count > 1))
                {
                    duplicateGroups++;
                    log.Add($"رفرنس تکراری: {kv.Key.Replace("@" + kv.Value[0].CustomerId, "")} (مشتری {kv.Value[0].CustomerId}): سفارش‌های {string.Join(", ", kv.Value.Select(v => v.OrderId))}");
                }

                log.Insert(0, $"بررسی سفارش‌ها: {orders.Count} سفارش با ImportRef؛ {duplicateGroups} گروه با رفرنس تکراری برای همان مشتری (برای بررسی دستی).");
                TempData["MatchOrdersLog"] = string.Join("\n", log);
                TempData["Info"] = duplicateGroups == 0 ? "هیچ سفارش تکراری (همان رفرنس و مشتری) یافت نشد." : $"{duplicateGroups} گروه تکراری در لاگ نمایش داده شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MatchOrders failed");
                TempData["Error"] = "خطا در تطبیق سفارش‌ها: " + ex.Message;
                TempData["MatchOrdersLog"] = ex.ToString();
            }
            return RedirectToAction(nameof(Index));
        }



        public async Task<IActionResult> UpdateCurrencyIds()
        {
            try
            {

                var bankaccounts = _context.BankAccounts.ToList();
                foreach (var bankcaccount in bankaccounts)
                {
                    var currencyId = _context.Currencies.FirstOrDefault(c => c.Code == bankcaccount.CurrencyCode).Id;
                    bankcaccount.CurrencyId = currencyId;
                    _context.Update(bankcaccount);
                }
                _context.SaveChanges();
                return Ok("All CurrencyIds have been updated");
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
    }
}
