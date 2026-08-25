using System.Text.RegularExpressions;

namespace ForexExchange.Helpers
{
    public static class SafePlainTextHelper
    {
        public const string ValidationErrorMessage =
            "عنوان سند نباید شامل نقل‌قول ('), backslash (\\), خط جدید، یا تگ HTML باشد.";

        public const string AllowedPattern = @"^[^'\\\r\n<>]*$";

        private static readonly Regex HtmlTagPattern = new(
            @"<\s*\/?\s*[a-zA-Z][^>]*|<[^>]*>",
            RegexOptions.Compiled);

        public static bool IsValid(string? value) => GetValidationError(value) == null;

        public static string? GetValidationError(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (value.Contains('\''))
            {
                return ValidationErrorMessage;
            }

            if (value.Contains('\\'))
            {
                return ValidationErrorMessage;
            }

            if (value.Contains('\r') || value.Contains('\n'))
            {
                return ValidationErrorMessage;
            }

            if (value.Contains('<') || value.Contains('>'))
            {
                return ValidationErrorMessage;
            }

            if (value.Contains("&lt;", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("&gt;", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationErrorMessage;
            }

            if (HtmlTagPattern.IsMatch(value))
            {
                return ValidationErrorMessage;
            }

            return null;
        }

        public static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("'", string.Empty)
                .Replace("\\", string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("<", string.Empty)
                .Replace(">", string.Empty)
                .Replace("&lt;", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("&gt;", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }
    }
}
