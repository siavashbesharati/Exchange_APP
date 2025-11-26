/// <summary>
/// Currency-related helper methods for normalization and comparison
/// </summary>
public static class CurrencyHelper
{
    /// <summary>
    /// Normalizes currency code to uppercase and trims whitespace
    /// Handles null values safely
    /// </summary>
    /// <param name="currencyCode">The currency code to normalize</param>
    /// <returns>Normalized currency code in uppercase without whitespace</returns>
    public static string NormalizeCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return string.Empty;
            
        return currencyCode.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Checks if two currency codes match (case-insensitive)
    /// </summary>
    /// <param name="code1">First currency code</param>
    /// <param name="code2">Second currency code</param>
    /// <returns>True if codes match</returns>
    public static bool CurrencyCodesMatch(string code1, string code2)
    {
        return string.Equals(NormalizeCurrencyCode(code1), NormalizeCurrencyCode(code2), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes currency code with validation
    /// </summary>
    /// <param name="currencyCode">Currency code to normalize</param>
    /// <param name="defaultCode">Default value if input is invalid</param>
    /// <returns>Normalized currency code or default</returns>
    public static string NormalizeCurrencyCode(string currencyCode, string defaultCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        return string.IsNullOrEmpty(normalized) ? NormalizeCurrencyCode(defaultCode) : normalized;
    }

    /// <summary>
    /// Normalizes a list of currency codes
    /// </summary>
    /// <param name="currencyCodes">List of currency codes to normalize</param>
    /// <returns>List of normalized currency codes</returns>
    public static List<string> NormalizeCurrencyCodes(IEnumerable<string> currencyCodes)
    {
        if (currencyCodes == null)
            return new List<string>();

        return currencyCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(NormalizeCurrencyCode)
            .ToList();
    }

    /// <summary>
    /// Validates if a currency code is in correct format
    /// </summary>
    /// <param name="currencyCode">Currency code to validate</param>
    /// <returns>True if valid format</returns>
    public static bool IsValidCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return false;

        var normalized = NormalizeCurrencyCode(currencyCode);
        
        // Currency codes are typically 3 letters (USD, EUR, etc.)
        return normalized.Length == 3 && normalized.All(char.IsLetter);
    }

    /// <summary>
    /// Normalizes and validates currency code, throws exception if invalid
    /// </summary>
    /// <param name="currencyCode">Currency code to normalize and validate</param>
    /// <returns>Normalized currency code</returns>
    /// <exception cref="ArgumentException">Thrown if currency code is invalid</exception>
    public static string NormalizeAndValidateCurrencyCode(string currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Currency code cannot be null or empty");
            
        if (!IsValidCurrencyCode(normalized))
            throw new ArgumentException($"Invalid currency code format: {currencyCode}");

        return normalized;
    }

    /// <summary>
    /// Creates a case-insensitive comparer for currency codes
    /// </summary>
    public static IEqualityComparer<string> CreateCurrencyCodeComparer()
    {
        return StringComparer.OrdinalIgnoreCase;
    }

    /// <summary>
    /// Normalizes currency code for database queries (handles nulls for EF)
    /// </summary>
    public static string NormalizeForQuery(string currencyCode)
    {
        return NormalizeCurrencyCode(currencyCode) ?? string.Empty;
    }
}