using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ForexExchange.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Newtonsoft.Json;

namespace ForexExchange.Controllers
{
    [Staff]
    public class BankStatementsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IBankStatementService _bankStatementService;
        private readonly ILogger<BankStatementsController> _logger;

        public BankStatementsController(
            ForexDbContext context,
            IBankStatementService bankStatementService,
            ILogger<BankStatementsController> logger)
        {
            _context = context;
            _bankStatementService = bankStatementService;
            _logger = logger;
        }

        // GET: BankStatements
        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers;
            return View();
        }

        // POST: BankStatements/Process
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int customerId, IFormFile bankStatementImage)
        {
            try
            {
                if (bankStatementImage == null || bankStatementImage.Length == 0)
                {
                    TempData["ErrorMessage"] = "لطفاً تصویر گردش حساب را انتخاب کنید.";
                    return RedirectToAction(nameof(Index));
                }

                if (customerId == 0)
                {
                    TempData["ErrorMessage"] = "لطفاً مشتری را انتخاب کنید.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(bankStatementImage.ContentType.ToLower()))
                {
                    TempData["ErrorMessage"] = "فرمت فایل پشتیبانی نمی‌شود. لطفاً تصویر JPG, PNG یا GIF آپلود کنید.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate file size (max 10MB)
                if (bankStatementImage.Length > 10 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "حجم فایل نباید بیشتر از ۱۰ مگابایت باشد.";
                    return RedirectToAction(nameof(Index));
                }

                // Convert to byte array
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await bankStatementImage.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Process the bank statement
                var analysis = await _bankStatementService.ProcessBankStatementAsync(imageData, customerId);

                if (!analysis.Success)
                {
                    TempData["ErrorMessage"] = $"خطا در پردازش گردش حساب: {analysis.ErrorMessage}";
                    return RedirectToAction(nameof(Index));
                }

                // Store the analysis result in TempData for the result view
                TempData["AnalysisResult"] = Newtonsoft.Json.JsonConvert.SerializeObject(analysis);
                TempData["SuccessMessage"] = "گردش حساب با موفقیت پردازش شد.";

                return RedirectToAction(nameof(Results));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bank statement for customer {CustomerId}", customerId);
                TempData["ErrorMessage"] = "خطا در پردازش گردش حساب.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: BankStatements/Results
        public IActionResult Results()
        {
            if (TempData["AnalysisResult"] == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var analysisJson = TempData["AnalysisResult"]?.ToString();
            if (string.IsNullOrEmpty(analysisJson))
            {
                return RedirectToAction(nameof(Index));
            }

            var analysis = Newtonsoft.Json.JsonConvert.DeserializeObject<BankStatementAnalysis>(analysisJson);
            if (analysis == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(analysis);
        }

        // POST: BankStatements/ConfirmMatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmMatch(int transactionId, int bankTransactionIndex)
        {
            try
            {
                // Here you would implement the logic to confirm a transaction match
                // TODO: Implement confirmation logic with AccountingDocuments in new architecture
                /*
                var transaction = await _context.Transactions.FindAsync(transactionId);
                if (transaction != null)
                {
                    // Add a note about bank statement verification
                    transaction.Notes = (transaction.Notes ?? "") + $" | تأیید از گردش حساب در تاریخ {DateTime.Now:yyyy/MM/dd}";
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "تطبیق تراکنش با گردش حساب تأیید شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "تراکنش یافت نشد.";
                }
                */

                TempData["SuccessMessage"] = "این قابلیت در حال توسعه است.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming transaction match");
                TempData["ErrorMessage"] = "خطا در تأیید تطبیق.";
                return RedirectToAction(nameof(Index));
            }
        }

        // API: Get customer pending transactions
        [HttpGet]
        public async Task<IActionResult> GetCustomerTransactions(int customerId)
        {
            try
            {
                // TODO: Implement with AccountingDocuments in new architecture
                /*
                var transactions = await _context.Transactions
                    .Include(t => t.FromCurrency)
                    .Include(t => t.ToCurrency)
                    .Where(t => (t.BuyerCustomerId == customerId || t.SellerCustomerId == customerId) &&
                               (t.Status == TransactionStatus.Pending || 
                                t.Status == TransactionStatus.PaymentUploaded ||
                                t.Status == TransactionStatus.ReceiptConfirmed))
                    .Select(t => new
                    {
                        id = t.Id,
                        amount = t.TotalInToman,
                        fromCurrency = t.FromCurrency!.Code,
                        toCurrency = t.ToCurrency!.Code,
                        status = t.Status.ToString(),
                        createdAt = t.CreatedAt.ToString("yyyy/MM/dd HH:mm")
                    })
                    .ToListAsync();
                */

                // For now return empty results
                var transactions = new List<dynamic>();

                return Json(transactions);
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Error getting customer transactions");
                return BadRequest();
            }
        }
    }
}
