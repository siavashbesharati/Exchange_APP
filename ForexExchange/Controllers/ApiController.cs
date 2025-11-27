using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ForexDbContext _context;

        public ApiController(ForexDbContext context)
        {
            _context = context;
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers
                    .Where(c => c.IsActive && !c.IsSystem)
                    .Select(c => new { id = c.Id, fullName = c.FullName })
                    .OrderBy(c => c.fullName)
                    .ToListAsync();

                return Ok(customers);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }

        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies()
        {
            try
            {
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new { id = c.Id, name = c.Name, code = c.Code , ratePriority = c.RatePriority})
                    .ToListAsync();

                return Ok(currencies);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }

        [HttpGet("bankaccounts")]
        public async Task<IActionResult> GetBankAccounts()
        {
            try
            {
                var bankAccounts = await _context.BankAccounts
                    .Where(ba => ba.IsActive)
                    .Select(ba => new { 
                        id = ba.Id, 
                        bankName = ba.BankName, 
                        accountNumber = ba.AccountNumber,
                        currencyCode = ba.CurrencyCode
                    })
                    .OrderBy(ba => ba.bankName)
                    .ToListAsync();

                return Ok(bankAccounts);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }

        [HttpGet("exchangerate/get")]
        public async Task<IActionResult> GetExchangeRate(int fromCurrencyId, int toCurrencyId)
        {
            try
            {
                // Try to find direct exchange rate
                var directRate = await _context.ExchangeRates
                    .Where(r => r.FromCurrencyId == fromCurrencyId &&
                               r.ToCurrencyId == toCurrencyId &&
                               r.IsActive)
                    .Select(r => new { r.Rate })
                    .FirstOrDefaultAsync();

                decimal rate = 0;
                string source = "";

                if (directRate != null)
                {
                    rate = directRate.Rate;
                    source = "Direct";
                }
                else
                {
                    // Try reverse rate
                    var reverseRate = await _context.ExchangeRates
                        .Where(r => r.FromCurrencyId == toCurrencyId &&
                                   r.ToCurrencyId == fromCurrencyId &&
                                   r.IsActive)
                        .Select(r => new { r.Rate })
                        .FirstOrDefaultAsync();

                    if (reverseRate != null)
                    {
                        rate = (1.0m / reverseRate.Rate);
                        source = "Reverse";
                    }
                    else
                    {
                        // Try cross-rate via base currency (IRR)
                        var baseCurrencyId = await _context.Currencies
                            .Where(c => c.Code == "IRR")
                            .Select(c => c.Id)
                            .FirstOrDefaultAsync();

                        if (baseCurrencyId > 0)
                        {
                            var fromRate = await _context.ExchangeRates
                                .Where(r => r.FromCurrencyId == baseCurrencyId &&
                                           r.ToCurrencyId == fromCurrencyId && r.IsActive)
                                .Select(r => new { r.Rate })
                                .FirstOrDefaultAsync();

                            var toRate = await _context.ExchangeRates
                                .Where(r => r.FromCurrencyId == baseCurrencyId &&
                                           r.ToCurrencyId == toCurrencyId && r.IsActive)
                                .Select(r => new { r.Rate })
                                .FirstOrDefaultAsync();

                            if (fromRate != null && toRate != null)
                            {
                                rate = toRate.Rate / fromRate.Rate;
                                source = "Cross-rate";
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    rate = rate,
                    source = source
                });
            }
            catch (Exception)
            {
                return Ok(new { success = false, error = "خطا در دریافت نرخ ارز" });
            }
        }

        [HttpGet("exchangerates/omr")]
        public async Task<IActionResult> GetOMRExchangeRates()
        {
            try
            {
                // Get OMR currency
                var omrCurrency = await _context.Currencies
                    .Where(c => c.Code == "OMR" && c.IsActive)
                    .FirstOrDefaultAsync();

                if (omrCurrency == null)
                {
                    return Ok(new List<object>());
                }

                // Get all active exchange rates from OMR to other currencies
                var rates = await _context.ExchangeRates
                    .Include(r => r.FromCurrency)
                    .Include(r => r.ToCurrency)
                    .Where(r => r.FromCurrencyId == omrCurrency.Id && 
                               r.IsActive &&
                               r.ToCurrency.IsActive)
                    .OrderBy(r => r.ToCurrency.DisplayOrder)
                    .ThenBy(r => r.ToCurrency.Code)
                    .Select(r => new
                    {
                        fromCurrency = r.FromCurrency.Code,
                        toCurrency = r.ToCurrency.Code,
                        toCurrencyName = r.ToCurrency.PersianName ?? r.ToCurrency.Name ?? r.ToCurrency.Code,
                        rate = r.Rate
                    })
                    .ToListAsync();

                return Ok(rates);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }
    }
}
