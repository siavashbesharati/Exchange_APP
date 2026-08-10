using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ForexExchange.Extensions
{
    /// <summary>
    /// Renders currency Code next to Symbol (logo image or legacy text).
    /// </summary>
    public static class CurrencyDisplayExtensions
    {
        /// <summary>
        /// Output: CODE + logo from /Currencies/Logo/{code}
        /// </summary>
        public static IHtmlContent CurrencyCodeWithSymbol(this IHtmlHelper html, string? code, int height = 16)
        {
            if (string.IsNullOrWhiteSpace(code))
                return HtmlString.Empty;

            var safeCode = code.Trim().ToUpperInvariant();
            var encodedCode = HtmlEncoder.Default.Encode(safeCode);
            var logoUrl = $"/Currencies/Logo/{Uri.EscapeDataString(safeCode)}";

            return new HtmlString(
                $"<span class=\"currency-code-display\" data-currency-code=\"{encodedCode}\" data-enhanced=\"1\">" +
                $"<span class=\"currency-code-text\">{encodedCode}</span> " +
                $"<img class=\"currency-logo\" src=\"{logoUrl}\" alt=\"\" height=\"{height}\" " +
                $"style=\"height:{height}px;width:auto;vertical-align:middle;\" " +
                $"onerror=\"this.style.display='none'\" loading=\"lazy\" />" +
                $"</span>");
        }

        /// <summary>
        /// When Symbol is already loaded — avoids an extra image request.
        /// </summary>
        public static IHtmlContent CurrencyCodeWithSymbol(this IHtmlHelper html, string? code, string? symbol, int height = 16)
        {
            if (string.IsNullOrWhiteSpace(code))
                return HtmlString.Empty;

            if (string.IsNullOrWhiteSpace(symbol))
                return html.CurrencyCodeWithSymbol(code, height);

            var safeCode = code.Trim().ToUpperInvariant();
            var encodedCode = HtmlEncoder.Default.Encode(safeCode);

            string symbolHtml;
            if (IsImageSymbol(symbol))
            {
                var safeSrc = HtmlEncoder.Default.Encode(symbol);
                symbolHtml =
                    $"<img class=\"currency-logo\" src=\"{safeSrc}\" alt=\"\" height=\"{height}\" " +
                    $"style=\"height:{height}px;width:auto;vertical-align:middle;\" />";
            }
            else
            {
                symbolHtml = $"<span class=\"currency-symbol-text\">{HtmlEncoder.Default.Encode(symbol)}</span>";
            }

            return new HtmlString(
                $"<span class=\"currency-code-display\" data-currency-code=\"{encodedCode}\" data-enhanced=\"1\">" +
                $"<span class=\"currency-code-text\">{encodedCode}</span> {symbolHtml}</span>");
        }

        private static bool IsImageSymbol(string? symbol) =>
            !string.IsNullOrWhiteSpace(symbol) &&
            symbol.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);
    }
}
