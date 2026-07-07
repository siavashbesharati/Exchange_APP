using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using ForexExchange.Extensions;
using ForexExchange.Authorization;

namespace ForexExchange.Controllers
{
    [HasPermission(Permissions.Exchange_Rates_Management)] 

    public class ExchangeRatesController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ExchangeRatesController> _logger;
        private readonly IWebScrapingService _webScrapingService;
        private readonly AdminActivityService _adminActivityService;
        private readonly AdminNotificationService _adminNotificationService;
        private readonly UserManager<ApplicationUser> _userManager;
    private const string IrrCurrencyCode = "IRR";

        public ExchangeRatesController(
            ForexDbContext context,
            ILogger<ExchangeRatesController> logger,
            IWebScrapingService webScrapingService,
            AdminActivityService adminActivityService,
            AdminNotificationService adminNotificationService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _adminActivityService = adminActivityService;
            _adminNotificationService = adminNotificationService;
            _userManager = userManager;
        }

        // GET: ExchangeRates
        public async Task<IActionResult> Index()
        {
            var exchangeRates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .OrderBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.Code)
                .ToListAsync();

            return View(exchangeRates);
        }





        // GET: API endpoint for current rates
        [HttpGet]
        public async Task<IActionResult> GetCurrentRates()
        {
            var rates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .Select(r => new
                {
                    fromCurrency = r.FromCurrency.Code,
                    toCurrency = r.ToCurrency.Code,
                    rate = r.Rate,
                    updatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Json(rates);
        }

        // GET: ExchangeRates/Manage
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public async Task<IActionResult> Manage(long? refresh)
        {
            // Force fresh query to avoid EF tracking cache issues
            var exchangeRates = await _context.ExchangeRates
                .AsNoTracking()
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .OrderBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.Code)
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive && c.Code != "OMR")
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Id, c.Code, c.PersianName })
                .ToListAsync();

            return View(exchangeRates);
        }

        // POST: ExchangeRates/UpdateAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public async Task<IActionResult> UpdateAll(Dictionary<int, decimal> rates)
        {
            if (rates == null)
            {
                TempData["ErrorMessage"] = "داده‌های ورودی نامعتبر است.";
                return RedirectToAction(nameof(Manage));
            }

            // Get base currency (OMR)
            var baseCurrency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Code == "OMR");

            if (baseCurrency == null)
            {
                TempData["ErrorMessage"] = "ارز پایه (ریال عمان) در پایگاه داده یافت نشد";
                return RedirectToAction(nameof(Manage));
            }

            var currencies = await _context.Currencies.Where(c => c.IsActive && c.Code != "OMR")
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            await UpdateBaseAndReverseRates(baseCurrency, currencies, rates);
            await UpdateCrossRates(baseCurrency, currencies, rates);
            TempData["SuccessMessage"] = "نرخ‌های ارز با موفقیت بروزرسانی شدند.";
            return RedirectToAction(nameof(Manage));
        }

        /// <summary>
        /// Updates direct and reverse rates for OMR and all active currencies.
        /// For each currency, sets both X→OMR and OMR→X to the same rate (no inversion).
        /// </summary>
        private async Task UpdateBaseAndReverseRates(Currency baseCurrency, List<Currency> currencies, Dictionary<int, decimal> rates)
        {
            foreach (var currency in currencies)
            {
                var currencyKey = currency.Id;
                if (!rates.ContainsKey(currencyKey))
                    continue;

                var newRate = rates[currencyKey];
                var adjustedRate = ApplyCurrencyTruncation(newRate, currency, baseCurrency);
                // X → OMR
                var direct = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.ToCurrencyId == baseCurrency.Id && r.IsActive);
                if (direct != null)
                {
                    direct.Rate = adjustedRate;
                    direct.UpdatedAt = DateTime.Now;
                    direct.UpdatedBy = User.Identity?.Name ?? "System";
                    _context.Update(direct);
                }
                else
                {
                    var newDirect = new ExchangeRate
                    {
                        Rate = adjustedRate,
                        FromCurrencyId = currency.Id,
                        ToCurrencyId = baseCurrency.Id,
                        IsActive = true,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "System"
                    };
                    _context.Add(newDirect);
                }

                // OMR → X (reverse, same rate)
                var reverse = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.FromCurrencyId == baseCurrency.Id && r.ToCurrencyId == currency.Id && r.IsActive);
                if (reverse != null)
                {
                    reverse.Rate = adjustedRate;
                    reverse.UpdatedAt = DateTime.Now;
                    reverse.UpdatedBy = User.Identity?.Name ?? "System";
                    _context.Update(reverse);
                }
                else
                {
                    var newReverse = new ExchangeRate
                    {
                        Rate = adjustedRate,
                        FromCurrencyId = baseCurrency.Id,
                        ToCurrencyId = currency.Id,
                        IsActive = true,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "System"
                    };
                    _context.Add(newReverse);
                }
            }
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Creates cross rates between non-OMR currencies using OMR as the base reference.
        /// Cross rate equals bigger base rate divided by smaller base rate, applied in both directions.
        /// </summary>
        private async Task UpdateCrossRates(Currency baseCurrency, List<Currency> currencies, Dictionary<int, decimal> rates)
        {
            if (baseCurrency == null)
                return;

            if (currencies.Count < 2)
                return;

            var currencyIds = currencies.Select(c => c.Id).ToList();

            var existingCrossRates = await _context.ExchangeRates
                .Where(r => r.IsActive && currencyIds.Contains(r.FromCurrencyId) && currencyIds.Contains(r.ToCurrencyId))
                .ToDictionaryAsync(r => (r.FromCurrencyId, r.ToCurrencyId));

            for (var i = 0; i < currencies.Count; i++)
            {
                var fromCurrency = currencies[i];
                if (!rates.TryGetValue(fromCurrency.Id, out var fromBaseRate) || fromBaseRate <= 0)
                    continue;

                for (var j = i + 1; j < currencies.Count; j++)
                {
                    var toCurrency = currencies[j];
                    if (!rates.TryGetValue(toCurrency.Id, out var toBaseRate) || toBaseRate <= 0)
                        continue;

                    var bigger = fromBaseRate >= toBaseRate ? fromBaseRate : toBaseRate;
                    var smaller = fromBaseRate >= toBaseRate ? toBaseRate : fromBaseRate;
                    if (smaller <= 0)
                        continue;

                    var crossRate = bigger / smaller;
                    var normalizedRate = ApplyCurrencyTruncation(crossRate, fromCurrency, toCurrency);

                    UpsertCrossRate(existingCrossRates, fromCurrency.Id, toCurrency.Id, normalizedRate);
                    UpsertCrossRate(existingCrossRates, toCurrency.Id, fromCurrency.Id, normalizedRate);
                }
            }

            await _context.SaveChangesAsync();
        }

        private void UpsertCrossRate(Dictionary<(int FromCurrencyId, int ToCurrencyId), ExchangeRate> existingCrossRates,
            int fromCurrencyId,
            int toCurrencyId,
            decimal rate)
        {
            if (existingCrossRates.TryGetValue((fromCurrencyId, toCurrencyId), out var existing))
            {
                existing.Rate = rate;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = User.Identity?.Name ?? "System";
                _context.Update(existing);
            }
            else
            {
                var newRate = new ExchangeRate
                {
                    Rate = rate,
                    FromCurrencyId = fromCurrencyId,
                    ToCurrencyId = toCurrencyId,
                    IsActive = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = User.Identity?.Name ?? "System"
                };
                _context.Add(newRate);
                existingCrossRates[(fromCurrencyId, toCurrencyId)] = newRate;
            }
        }

        private static decimal ApplyCurrencyTruncation(decimal value, Currency currencyA, Currency currencyB)
        {
            var code = IsIrr(currencyA) || IsIrr(currencyB) ? IrrCurrencyCode : null;
            return value.TruncateToCurrencyDefaults(code);
        }

        private static bool IsIrr(Currency currency)
        {
            return string.Equals(currency.Code, IrrCurrencyCode, StringComparison.OrdinalIgnoreCase);
        }

        // POST: ExchangeRates/UpdateFromWeb
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public IActionResult UpdateFromWeb()
        {
            // DISABLED: Web scraping functionality
            TempData["ErrorMessage"] = "بروزرسانی از وب غیرفعال شده است.";
            return RedirectToAction(nameof(Manage), new { refresh = DateTime.Now.Ticks });
        }

        private bool ExchangeRateExists(int id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}
