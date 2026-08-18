using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services.Notifications.Helpers;

namespace ForexExchange.Services.Notifications
{
    public class TelegramBotCommandService
    {
        private readonly ForexDbContext _context;

        public TelegramBotCommandService(ForexDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<string>> BuildRatesMessagesAsync(
            CancellationToken cancellationToken = default
        )
        {
            var rates = await _context.ExchangeRates
                .AsNoTracking()
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive && r.FromCurrency.IsActive && r.ToCurrency.IsActive)
                .OrderBy(r => r.FromCurrency.DisplayOrder)
                .ThenBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.DisplayOrder)
                .ThenBy(r => r.ToCurrency.Code)
                .Select(r => new TelegramRateRow
                {
                    FromCurrency = r.FromCurrency.Code,
                    ToCurrency = r.ToCurrency.Code,
                    Rate = r.Rate,
                    UpdatedAt = r.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            return TelegramRatesMessageFormatter.Format(rates);
        }
    }
}
