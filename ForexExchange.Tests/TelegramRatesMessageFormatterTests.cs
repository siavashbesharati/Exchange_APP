using ForexExchange.Services.Notifications.Helpers;

namespace ForexExchange.Tests
{
    public class TelegramRatesMessageFormatterTests
    {
        [Fact]
        public void Format_EmptyRates_ReturnsNoRatesMessage()
        {
            var messages = TelegramRatesMessageFormatter.Format(Array.Empty<TelegramRateRow>());

            Assert.Single(messages);
            Assert.Contains("نرخ فعالی ثبت نشده است", messages[0]);
        }

        [Fact]
        public void Format_Rates_IncludesPairsAndFormattedRate()
        {
            var rates = new[]
            {
                new TelegramRateRow
                {
                    FromCurrency = "USD",
                    ToCurrency = "IRR",
                    Rate = 865000m,
                    UpdatedAt = new DateTime(2026, 8, 18, 17, 56, 0),
                },
                new TelegramRateRow
                {
                    FromCurrency = "EUR",
                    ToCurrency = "IRR",
                    Rate = 942000.50m,
                    UpdatedAt = new DateTime(2026, 8, 18, 16, 0, 0),
                },
            };

            var messages = TelegramRatesMessageFormatter.Format(rates);

            Assert.Single(messages);
            Assert.Contains("USD / IRR: <b>865,000</b>", messages[0]);
            Assert.Contains("EUR / IRR: <b>942,000.5</b>", messages[0]);
            Assert.Contains("2026-08-18 17:56", messages[0]);
        }

        [Fact]
        public void FormatHelp_ListsRatesCommand()
        {
            var help = TelegramRatesMessageFormatter.FormatHelp();

            Assert.Contains("/rates", help);
            Assert.Contains("/help", help);
        }
    }
}
