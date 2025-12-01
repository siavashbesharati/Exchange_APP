using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Extensions;

namespace ForexExchange.Controllers
{
    [Authorize]
    public class CustomerFinancialHistoryController : Controller
    {
        private readonly CustomerFinancialHistoryService _historyService;
        private readonly ForexDbContext _context;
        private readonly ILogger<CustomerFinancialHistoryController> _logger;

        public CustomerFinancialHistoryController(
            CustomerFinancialHistoryService historyService,
            ForexDbContext context,
            ILogger<CustomerFinancialHistoryController> logger)
        {
            _historyService = historyService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Customer Financial History main page
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Get complete customer financial timeline
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomerTimeline(int customerId, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null, int page = 1, int pageSize = 10)
        {
            try
            {
                if (customerId <= 0)
                    return BadRequest("Invalid customer ID");

                var timeline = await _historyService.GetCustomerTimelineAsync(customerId, fromDate, toDate, currencyId);
                
                // Apply pagination to transactions
                if (timeline?.Transactions != null)
                {
                    var totalTransactions = timeline.Transactions.Count;
                    var totalPages = (int)Math.Ceiling((double)totalTransactions / pageSize);
                    
                    var pagedTransactions = timeline.Transactions
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                    
                    timeline.Transactions = pagedTransactions;
                    
                    return Json(new { 
                        success = true, 
                        data = timeline,
                        pagination = new {
                            currentPage = page,
                            totalPages = totalPages,
                            totalRecords = totalTransactions,
                            pageSize = pageSize
                        }
                    });
                }
                
                return Json(new { success = true, data = timeline });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer timeline for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "خطا در دریافت تاریخچه مالی مشتری" });
            }
        }

        /// <summary>
        /// Get balance snapshot at specific date
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBalanceSnapshot(int customerId, DateTime asOfDate)
        {
            try
            {
                if (customerId <= 0)
                    return BadRequest("Invalid customer ID");

                var snapshot = await _historyService.GetBalanceSnapshotAsync(customerId, asOfDate);
                return Json(new { success = true, data = snapshot });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance snapshot for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "خطا در دریافت وضعیت موجودی" });
            }
        }

        /// <summary>
        /// Get customer transaction statistics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomerStats(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                if (customerId <= 0)
                    return BadRequest("Invalid customer ID");

                var stats = await _historyService.GetCustomerStatsAsync(customerId, fromDate, toDate);
                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer stats for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "خطا در دریافت آمار مشتری" });
            }
        }

        /// <summary>
        /// Get currency-specific transaction summary
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCurrencyTransactionSummary(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                if (customerId <= 0)
                    return BadRequest("Invalid customer ID");

                var summary = await _historyService.GetCurrencyTransactionSummaryAsync(customerId, fromDate, toDate);
                return Json(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting currency summary for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "خطا در دریافت خلاصه ارزی" });
            }
        }

        /// <summary>
        /// Customer Financial Timeline View
        /// </summary>
        public async Task<IActionResult> Timeline(int id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                    return NotFound();

                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading timeline view for customer {CustomerId}", id);
                return RedirectToAction("Index", "Customers");
            }
        }

        /// <summary>
        /// Export customer financial timeline to Excel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportTimeline(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var timeline = await _historyService.GetCustomerTimelineAsync(customerId, fromDate, toDate, null);
                
                // Create Excel export logic here
                // For now, return JSON for testing
                return Json(new { 
                    success = true, 
                    message = "Export functionality will be implemented",
                    data = new { 
                        customerName = timeline.CustomerName,
                        transactionCount = timeline.TotalTransactions,
                        dateRange = $"{timeline.FromDate:yyyy-MM-dd} to {timeline.ToDate:yyyy-MM-dd}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting timeline for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "خطا در دریافت فایل" });
            }
        }

        /// <summary>
        /// Display customer timeline in bank receipt format
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PrintFinancialReport(int customerId, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
        {
            try
            {
                if (customerId <= 0)
                    return BadRequest("Invalid customer ID");

                var timeline = await _historyService.GetCustomerTimelineAsync(customerId, fromDate, toDate, currencyId);
                
                if (timeline == null)
                    return NotFound("Timeline not found");

                // Convert CustomerFinancialTimeline to FinancialReportViewModel
                var transactions = timeline.Transactions.Select(t => new FinancialTransactionItem
                {
                    TransactionDate = t.TransactionDate,
                    TransactionType = t.Type.GetDisplayName(),
                    Description = t.Description,
                    Note = t.Notes ?? "",
                    CurrencyCode = t.CurrencyCode,
                    Amount = t.Amount,
                    RunningBalance = t.RunningBalance,
                    ReferenceId = t.ReferenceId,
                    CanNavigate = t.ReferenceId.HasValue,
                    TransactionNumber = t.TransactionNumber,
                    FromCurrency = t.FromCurrency,
                    ToCurrency = t.ToCurrency,
                    ExchangeRate = t.ExchangeRate
                }).ToList();

                var reportModel = new FinancialReportViewModel
                {
                    ReportType = "Customer",
                    EntityName = timeline.CustomerName,
                    EntityId = timeline.CustomerId,
                    FromDate = timeline.FromDate,
                    ToDate = timeline.ToDate,
                    Transactions = transactions,
                    FinalBalances = timeline.FinalBalances,
                    InitialBalances = timeline.InitialBalances,
                    ReportTitle = $"صورتحساب مشتری - {timeline.CustomerName}",
                    ReportSubtitle = $"از {timeline.FromDate.ToString("yyyy/MM/dd")} تا {timeline.ToDate.ToString("yyyy/MM/dd")}"
                };

                return View("~/Views/PrintViews/CustomerPrintReport.cshtml", reportModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bank receipt for customer {CustomerId}", customerId);
                return View("Error");
            }
        }
    }
}
