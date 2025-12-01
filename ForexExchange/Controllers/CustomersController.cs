using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Globalization;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Operator,Programmer")]
    public class CustomersController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CustomersController> _logger;
        private readonly CustomerDebtCreditService _debtCreditService;
        private readonly IShareableLinkService _shareableLinkService;
        private readonly AdminNotificationService _adminNotificationService;
        private readonly ICentralFinancialService _centralFinancialService;

        public CustomersController(
            ForexDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CustomersController> logger,
            CustomerDebtCreditService debtCreditService,
            IShareableLinkService shareableLinkService,
            AdminNotificationService adminNotificationService,
            ICentralFinancialService centralFinancialService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _debtCreditService = debtCreditService;
            _shareableLinkService = shareableLinkService;
            _adminNotificationService = adminNotificationService;
            _centralFinancialService = centralFinancialService;
        }        // GET: Customers
        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            return View(customers);
        }


        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var customer = new Customer
                {
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber,
                    Gender = model.Gender,
                    Address = model.Address ?? string.Empty,
                    IsActive = model.IsActive,
                    IsShareHolder = model.IsShareHolder,
                    CreatedAt = DateTime.Now
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();
            var model = new CustomerEditViewModel
            {
                Id = customer.Id,
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber,
                Gender = customer.Gender,
                Address = customer.Address,
                IsActive = customer.IsActive,
                IsSystem = customer.IsSystem,
                IsShareHolder = customer.IsShareHolder,
                CreatedAt = customer.CreatedAt
            };
            return View(model);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();
            if (ModelState.IsValid)
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                    return NotFound();
                customer.FullName = model.FullName;
                customer.PhoneNumber = model.PhoneNumber;
                customer.Gender = model.Gender;
                customer.Address = model.Address ?? string.Empty;
                customer.IsActive = model.IsActive;
                customer.IsSystem = model.IsSystem;
                customer.IsShareHolder = model.IsShareHolder;
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();
            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }





        private Dictionary<string, decimal> ExtractInitialBalancesFromForm()
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var form = Request?.Form ?? new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
                _logger.LogInformation($"=== EXTRACTING INITIAL BALANCES FROM FORM ===");
                _logger.LogInformation($"Form has {form.Count} total keys: {string.Join(", ", form.Keys)}");

                // Method 1: Try dictionary-style inputs (InitialBalances[CODE])
                _logger.LogInformation("Method 1: Looking for InitialBalances[CODE] inputs...");
                var dictionaryInputs = form.Keys.Where(k => k.StartsWith("InitialBalances[")).ToList();
                _logger.LogInformation($"Found {dictionaryInputs.Count} dictionary-style inputs: {string.Join(", ", dictionaryInputs)}");

                foreach (var name in dictionaryInputs)
                {
                    if (name.StartsWith("InitialBalances[", StringComparison.Ordinal) && name.EndsWith("]", StringComparison.Ordinal))
                    {
                        var inner = name.Substring("InitialBalances[".Length);
                        var code = inner.Substring(0, inner.Length - 1).Trim().ToUpperInvariant();
                        var raw = form[name].ToString().Trim();
                        _logger.LogInformation($"Processing dictionary input: {name} = '{raw}'");

                        if (string.IsNullOrWhiteSpace(code))
                        {
                            _logger.LogWarning($"Skipping empty currency code from {name}");
                            continue;
                        }

                        // Normalize Persian/Arabic digits and separators
                        raw = (raw ?? "").Replace("\u066C", "").Replace("\u066B", "."); // Arabic thousands/decimal
                        // If string has comma but no dot, treat comma as decimal separator; otherwise remove commas (thousands)
                        if (raw.Contains(',') && !raw.Contains('.'))
                            raw = raw.Replace(',', '.');
                        else
                            raw = raw.Replace(",", "");
                        raw = raw.Replace(" ", "");

                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                        {
                            // Preserve original value (including negative)

                            result[code] = amount;
                            _logger.LogInformation($"SUCCESS: Added {code} = {amount} from dictionary method");
                        }
                        else
                        {
                            _logger.LogWarning($"FAILED: Could not parse amount '{raw}' for currency {code}");
                        }
                    }
                }

                // Method 2: Try paired arrays (ib_code[], ib_amount[])
                _logger.LogInformation("Method 2: Looking for ib_code/ib_amount arrays...");
                var codes = form["ib_code"]; // multiple
                var amounts = form["ib_amount"]; // multiple

                _logger.LogInformation($"Found {codes.Count} codes and {amounts.Count} amounts");
                for (int i = 0; i < codes.Count && i < amounts.Count; i++)
                {
                    _logger.LogInformation($"Array item {i}: code='{codes[i]}', amount='{amounts[i]}'");
                }

                if (codes.Count > 0 && amounts.Count > 0)
                {
                    for (int i = 0; i < Math.Min(codes.Count, amounts.Count); i++)
                    {
                        var code = codes[i]?.Trim().ToUpperInvariant();
                        var raw = amounts[i]?.Trim();

                        _logger.LogInformation($"Processing array item {i}: code='{code}', amount='{raw}'");

                        if (string.IsNullOrWhiteSpace(code))
                        {
                            _logger.LogWarning($"Skipping empty currency code at index {i}");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            _logger.LogWarning($"Skipping empty amount for currency {code} at index {i}");
                            continue;
                        }

                        raw = (raw ?? "").Replace("\u066C", "").Replace("\u066B", ".");
                        if (raw.Contains(',') && !raw.Contains('.')) raw = raw.Replace(',', '.');
                        else raw = raw.Replace(",", "");
                        raw = raw.Replace(" ", "");

                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                        {
                            // Preserve original value (including negative)
                            if (!result.ContainsKey(code))
                            {
                                result[code] = amount;
                                _logger.LogInformation($"SUCCESS: Added from arrays {code} = {amount}");
                            }
                            else
                            {
                                _logger.LogInformation($"SKIPPED: {code} already exists from dictionary method");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"FAILED: Could not parse amount '{raw}' for currency {code}");
                        }
                    }
                }

                _logger.LogInformation($"=== FINAL RESULT: {result.Count} currencies ===");
                foreach (var kvp in result)
                {
                    _logger.LogInformation($"Final: {kvp.Key} = {kvp.Value}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractInitialBalancesFromForm");
            }
            return result;
        }



        // GET: API endpoint for customer search
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var customers = await _context.Customers
                .Where(c => c.IsActive &&
                           (c.FullName.Contains(term) || c.PhoneNumber.Contains(term)))
                .Select(c => new
                {
                    id = c.Id,
                    text = $"{c.FullName} - {c.PhoneNumber}"
                })
                .OrderBy(c => c.text)
                .Take(10)
                .ToListAsync();

            return Json(customers);
        }

        // POST: Customers/GenerateShareableLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateShareableLink(int customerId, ShareableLinkType linkType, int expirationDays = 7)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "مشتری یافت نشد.";
                    return RedirectToAction("Profile", new { id = customerId });
                }

                var currentUser = User.Identity?.Name ?? "Admin";
                var description = linkType switch
                {
                    ShareableLinkType.ComprehensiveStatement => "لینک اشتراک صورت حساب جامع",
                    ShareableLinkType.TransactionsStatement => "لینک اشتراک صورت حساب معاملات",
                    _ => "لینک اشتراک"
                };

                var shareableLink = await _shareableLinkService.GenerateLinkAsync(
                    customerId,
                    linkType,
                    expirationDays,
                    description,
                    currentUser);

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var fullUrl = shareableLink.GetShareableUrl(baseUrl);

                TempData["SuccessMessage"] = $"لینک اشتراک با موفقیت ایجاد شد. لینک تا {expirationDays} روز آینده معتبر است.";
                TempData["ShareableUrl"] = fullUrl;

                return RedirectToAction("ShareableLinks", new { id = customerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating shareable link for customer {CustomerId}", customerId);
                TempData["ErrorMessage"] = "خطا در ایجاد لینک اشتراک.";
                return RedirectToAction("ShareableLinks", new { id = customerId });
            }
        }

        // GET: Customers/ShareableLinks/5
        public async Task<IActionResult> ShareableLinks(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var links = await _shareableLinkService.GetCustomerLinksAsync(id, activeOnly: false);

            ViewBag.Customer = customer;
            return View(links);
        }

        // POST: Customers/DeactivateShareableLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateShareableLink(int linkId, int customerId)
        {
            try
            {
                var success = await _shareableLinkService.DeactivateLinkAsync(linkId, User.Identity?.Name);
                if (success)
                {
                    TempData["SuccessMessage"] = "لینک اشتراک با موفقیت غیرفعال شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "لینک یافت نشد.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating shareable link {LinkId}", linkId);
                TempData["ErrorMessage"] = "خطا در غیرفعال کردن لینک.";
            }

            return RedirectToAction("ShareableLinks", new { id = customerId });
        }

        // AJAX: Get customer balance data
        [HttpGet]
        public async Task<IActionResult> GetCustomerBalance(int id)
        {
            try
            {
                var balances = await _context.CustomerBalances
                    .Where(cb => cb.CustomerId == id)
                    .Select(cb => new
                    {
                        currencyId = cb.CurrencyId,
                        currencyCode = cb.Currency != null ? cb.Currency.Code : cb.CurrencyCode, // Display from navigation
                        balance = cb.Balance,
                        lastUpdated = cb.LastUpdated,
                        notes = cb.Notes
                    })
                    .ToListAsync();

                return Json(balances);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer balance for customer {CustomerId}", id);
                return Json(new { error = "خطا در بارگذاری اطلاعات موجودی" });
            }
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
