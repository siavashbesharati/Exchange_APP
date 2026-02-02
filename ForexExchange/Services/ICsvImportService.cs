namespace ForexExchange.Services
{
    /// <summary>
    /// Service for CSV import operations. Creates and uses dedicated temp bank accounts
    /// for document import only, keeping real bank accounts untouched until valid accounts are assigned.
    /// </summary>
    public interface ICsvImportService
    {
        /// <summary>
        /// Gets or creates a temp bank account for CSV import for the given currency.
        /// These accounts are used only by the CSV document import action so real bank accounts stay safe.
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <param name="currencyCode">Currency code (e.g. IRR)</param>
        /// <returns>The temp bank account (system customer, name "CSV Import Temp - {code}")</returns>
        Task<Models.BankAccount> GetOrCreateTempBankAccountForCsvImportAsync(int currencyId, string currencyCode);
    }
}
