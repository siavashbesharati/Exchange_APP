using System;
using System.Linq;
using ForexExchange.Extensions;
using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public interface ICurrencyConversionService
    {
        decimal ConvertAmount(decimal amount, int fromCurrencyId, int toCurrencyId);
    }

    public class CurrencyConversionService : ICurrencyConversionService
    {

        private readonly ForexDbContext _context;

        public CurrencyConversionService(ForexDbContext context)
        {
            _context = context;
        }
        public decimal ConvertAmount(decimal amount, int fromCurrencyId, int toCurrencyId)
        {
            if (amount == 0)
                return 0;

            if (fromCurrencyId == toCurrencyId)
                return amount;

            var fromCurrency = _context.Currencies
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == fromCurrencyId);
            var toCurrency = _context.Currencies
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == toCurrencyId);

            if (fromCurrency == null || toCurrency == null)
                return 0;

            if (TryConvertWithAvailableRate(amount, fromCurrency, toCurrency, out var directResult))
            {
                return ApplyCurrencyRules(directResult, toCurrency);
            }

            var baseCurrency = ResolveBaseCurrency(fromCurrency, toCurrency);
            if (baseCurrency != null)
            {
                if (TryConvertWithAvailableRate(amount, fromCurrency, baseCurrency, out var amountInBase))
                {
                    amountInBase = ApplyCurrencyRules(amountInBase, baseCurrency);

                    if (TryConvertWithAvailableRate(amountInBase, baseCurrency, toCurrency, out var finalAmount))
                    {
                        return ApplyCurrencyRules(finalAmount, toCurrency);
                    }
                }
            }

            return 0;
        }

        private bool TryConvertWithAvailableRate(decimal amount, Currency fromCurrency, Currency toCurrency, out decimal result)
        {
            result = 0;

            if (fromCurrency.Id == toCurrency.Id)
            {
                result = amount;
                return true;
            }

            var directRate = GetActiveRate(fromCurrency.Id, toCurrency.Id);
            var reverseRate = GetActiveRate(toCurrency.Id, fromCurrency.Id);
            var priorityComparison = fromCurrency.RatePriority.CompareTo(toCurrency.RatePriority);

            if (priorityComparison < 0)
            {
                if (directRate.HasValue)
                {
                    result = amount * directRate.Value;
                    return true;
                }
                if (reverseRate.HasValue)
                {
                    result = amount / reverseRate.Value;
                    return true;
                }
            }
            else if (priorityComparison > 0)
            {
                if (reverseRate.HasValue)
                {
                    result = amount / reverseRate.Value;
                    return true;
                }
                if (directRate.HasValue)
                {
                    result = amount / directRate.Value;
                    return true;
                }
            }
            else
            {
                if (directRate.HasValue)
                {
                    result = amount * directRate.Value;
                    return true;
                }
                if (reverseRate.HasValue)
                {
                    result = amount / reverseRate.Value;
                    return true;
                }
            }

            return false;
        }

        private decimal? GetActiveRate(int fromCurrencyId, int toCurrencyId)
        {
            var rate = _context.ExchangeRates
                .Where(r => r.FromCurrencyId == fromCurrencyId && r.ToCurrencyId == toCurrencyId && r.IsActive)
                .Select(r => (decimal?)r.Rate)
                .FirstOrDefault();

            if (!rate.HasValue || rate.Value <= 0)
                return null;

            return rate.Value;
        }

        private Currency? ResolveBaseCurrency(Currency fromCurrency, Currency toCurrency)
        {
            var preferredBases = new[] { "OMR", "IRR" };

            foreach (var code in preferredBases)
            {
                var candidate = _context.Currencies
                    .AsNoTracking()
                    .FirstOrDefault(c => c.Code == code && c.IsActive);
                if (candidate != null && candidate.Id != fromCurrency.Id && candidate.Id != toCurrency.Id)
                    return candidate;
            }

            return _context.Currencies
                .AsNoTracking()
                .Where(c => c.IsActive && c.Id != fromCurrency.Id && c.Id != toCurrency.Id)
                .OrderBy(c => c.RatePriority)
                .FirstOrDefault();
        }

        private static decimal ApplyCurrencyRules(decimal value, Currency targetCurrency)
        {
            var code = targetCurrency.Code;
            return value.TruncateToCurrencyDefaults(code);
        }
    }
}
