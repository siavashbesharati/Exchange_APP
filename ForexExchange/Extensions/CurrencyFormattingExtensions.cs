using System;
using System.Globalization;

namespace ForexExchange.Extensions
{
    /// <summary>
    /// Extension methods for formatting currency values with thousand separators
    /// </summary>
    public static class CurrencyFormattingExtensions
    {
        /// <summary>
        /// Format decimal value with thousand separators based on currency code.
        /// IRR = no decimals (truncate integer). Non-IRR = 2 decimals (standard rounding).
        /// Trailing zeros after decimal point are removed: 23.60 → 23.6, 23.00 → 23
        /// Examples: 1.569 USD → "1.57"  |  1.564 USD → "1.56"  |  55000 IRR → "55,000"
        /// </summary>
        public static string FormatCurrency(this decimal value, string? currencyCode = null)
        {
            if (currencyCode == "IRR")
            {
                return Math.Truncate(value).ToString("N0", CultureInfo.InvariantCulture);
            }

            var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            var formatted = rounded.ToString("N2", CultureInfo.InvariantCulture);

            // Remove trailing zeros: 23.60 → 23.6, 23.00 → 23
            if (formatted.Contains('.'))
            {
                formatted = formatted.TrimEnd('0').TrimEnd('.');
            }

            return formatted;
        }

        /// <summary>
        /// Round amount to display precision for sign/badge checks.
        /// IRR = integer truncation. Non-IRR = round to 2 decimal places.
        /// Keeps badge logic consistent with what FormatCurrency displays.
        /// </summary>
        public static decimal TruncateCurrencyAmount(this decimal value, string? currencyCode = null)
        {
            if (currencyCode == "IRR")
                return Math.Truncate(value);

            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Format double value with thousand separators based on currency code
        /// GLOBAL FORMATTING RULE: IRR = no decimals (truncate), non-IRR = 2 decimals (truncate)
        /// Trailing zeros after decimal point are removed: 23.60 → 23.6, 23.00 → 23
        /// </summary>
        /// <param name="value">The double value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this double value, string? currencyCode = null)
        {
            return ((decimal)value).FormatCurrency(currencyCode);
        }

        /// <summary>
        /// Format float value with thousand separators based on currency code
        /// GLOBAL FORMATTING RULE: IRR = no decimals (truncate), non-IRR = 2 decimals (truncate)
        /// Trailing zeros after decimal point are removed: 23.60 → 23.6, 23.00 → 23
        /// </summary>
        /// <param name="value">The float value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this float value, string? currencyCode = null)
        {
            return ((decimal)value).FormatCurrency(currencyCode);
        }

        /// <summary>
        /// Format integer value with thousand separators
        /// </summary>
        /// <param name="value">The integer value to format</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format long value with thousand separators
        /// </summary>
        /// <param name="value">The long value to format</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format nullable decimal value with thousand separators
        /// </summary>
        /// <param name="value">The nullable decimal value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators, or empty string if null</returns>
        public static string FormatCurrency(this decimal? value, string? currencyCode = null)
        {
            return value?.FormatCurrency(currencyCode) ?? "";
        }

        /// <summary>
        /// Format nullable double value with thousand separators
        /// </summary>
        /// <param name="value">The nullable double value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators, or empty string if null</returns>
        public static string FormatCurrency(this double? value, string? currencyCode = null)
        {
            return value?.FormatCurrency(currencyCode) ?? "";
        }

        /// <summary>
        /// Format an exchange rate with thousand separators, preserving ALL stored decimal places.
        /// Never rounds or truncates — only adds thousand separators and strips trailing zeros.
        /// Use this everywhere a Rate field (Order.Rate, ExchangeRate.Rate) is displayed.
        /// Example: 1234567.8900 → "1,234,567.89"
        ///          55000.0000   → "55,000"
        /// </summary>
        public static string FormatRate(this decimal value)
        {
            // Strip trailing zeros from the stored decimal
            // G29 gives full precision without scientific notation
            var raw = value.ToString("G29", CultureInfo.InvariantCulture);

            // Parse back so we can use Intl formatting via ToString("N") trick:
            // Build the formatted string with thousand separators
            var parts = raw.Split('.');
            var intPart = long.Parse(parts[0], CultureInfo.InvariantCulture);
            var intFormatted = intPart.ToString("N0", CultureInfo.InvariantCulture);

            if (parts.Length == 1 || string.IsNullOrEmpty(parts[1]))
                return intFormatted;

            // Trim trailing zeros from decimal part
            var decPart = parts[1].TrimEnd('0');
            return string.IsNullOrEmpty(decPart) ? intFormatted : $"{intFormatted}.{decPart}";
        }

        /// <summary>
        /// Nullable overload for FormatRate.
        /// </summary>
        public static string FormatRate(this decimal? value)
        {
            return value?.FormatRate() ?? "";
        }

        /// <summary>
        /// Truncate a decimal value based on currency-specific rules (NO ROUNDING).
        /// For IRR, truncates all decimal places. For others, truncates to 2 decimal places.
        /// This affects the actual value, not just the display format.
        /// </summary>
        /// <param name="value">The decimal value to truncate.</param>
        /// <param name="currencyCode">The currency code (e.g., "IRR").</param>
        /// <returns>The truncated decimal value.</returns>
        public static decimal TruncateToCurrencyDefaults(this decimal value, string? currencyCode)
        {
            if (currencyCode == "IRR")
            {
                // For IRR, truncate all decimal places (no rounding)
                return Math.Truncate(value);
            }
            else
            {
                // For other currencies, truncate to exactly 2 decimal places (no rounding)
                return Math.Truncate(value * 100) / 100;
            }
        }
    }
}
