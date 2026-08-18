using System.Net;
using System.Text;
using ForexExchange.Extensions;
using ForexExchange.Helpers;

namespace ForexExchange.Services.Notifications.Helpers
{
    public sealed class TelegramRateRow
    {
        public required string FromCurrency { get; init; }
        public required string ToCurrency { get; init; }
        public required decimal Rate { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public static class TelegramRatesMessageFormatter
    {
        private const string Divider = "━━━━━━━━━━━━━━━━━━";
        private const int TelegramMessageLimit = 3500;

        public static IReadOnlyList<string> Format(IReadOnlyList<TelegramRateRow> rates)
        {
            if (rates.Count == 0)
            {
                return new[]
                {
                    "<b>💱 نرخ‌های فعال</b>\n"
                        + Divider
                        + "\nنرخ فعالی ثبت نشده است.",
                };
            }

            var latestUpdate = rates.Max(r => r.UpdatedAt);
            var lines = rates
                .Select(r =>
                    $"{Escape(r.FromCurrency)} / {Escape(r.ToCurrency)}: <b>{Escape(r.Rate.FormatRate())}</b>"
                )
                .ToList();

            var header =
                "<b>💱 نرخ‌های فعال</b>\n" + Divider + "\n";
            var footer =
                "\n"
                + Divider
                + $"\n🕐 آخرین بروزرسانی: <b>{Escape(latestUpdate.ToDisplayDateTime())}</b>";

            return Chunk(header, lines, footer);
        }

        public static string FormatHelp()
        {
            return "<b>ربات اعلان‌های صرافی</b>\n"
                + Divider
                + "\nدستورات:\n"
                + "/rates — نرخ‌های فعال ارز\n"
                + "/help — همین راهنما";
        }

        private static IReadOnlyList<string> Chunk(
            string header,
            IReadOnlyList<string> lines,
            string footer
        )
        {
            var chunks = new List<string>();
            var current = new StringBuilder(header);

            foreach (var line in lines)
            {
                var candidateLength = current.Length + line.Length + 1 + footer.Length;
                if (current.Length > header.Length && candidateLength > TelegramMessageLimit)
                {
                    current.Append(footer);
                    chunks.Add(current.ToString());
                    current = new StringBuilder(header);
                }

                current.AppendLine(line);
            }

            current.Append(footer);
            chunks.Add(current.ToString());
            return chunks;
        }

        private static string Escape(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "—");
        }
    }
}
