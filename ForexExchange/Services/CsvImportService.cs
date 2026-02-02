using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    /// <summary>
    /// Service for CSV import. Creates and uses dedicated temp bank accounts for document import only,
    /// keeping real bank accounts untouched until valid accounts are assigned.
    /// </summary>
    public class CsvImportService : ICsvImportService
    {
        private const string TempAccountNumberPrefix = "IMPORT-TEMP-";
        private const string TempBankNamePrefix = "CSV Import Temp - ";
        private const string TempAccountHolderName = "موقت ورود CSV";
        private const string TempNotes = "حساب موقت برای ورود CSV - تا تعیین حساب بانکی معتبر";

        private readonly ForexDbContext _context;
        private readonly ILogger<CsvImportService> _logger;

        public CsvImportService(ForexDbContext context, ILogger<CsvImportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<BankAccount> GetOrCreateTempBankAccountForCsvImportAsync(int currencyId, string currencyCode)
        {
            var code = (currencyCode ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Currency code is required for temp bank account.", nameof(currencyCode));

            var systemCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.IsSystem);

            if (systemCustomer == null)
                throw new InvalidOperationException("System customer not found. Run data seed to create system customer.");

            var accountNumber = TempAccountNumberPrefix + code;
            var existing = await _context.BankAccounts
                .FirstOrDefaultAsync(ba =>
                    ba.CustomerId == systemCustomer.Id &&
                    ba.AccountNumber == accountNumber);

            if (existing != null)
            {
                _logger.LogDebug("Using existing temp bank account for CSV import: {AccountNumber} (Id={Id})", accountNumber, existing.Id);
                return existing;
            }

            var tempAccount = new BankAccount
            {
                CustomerId = systemCustomer.Id,
                BankName = TempBankNamePrefix + code,
                AccountNumber = accountNumber,
                AccountHolderName = TempAccountHolderName,
                CurrencyCode = code,
                CurrencyId = currencyId,
                IsActive = true,
                IsDefault = false,
                Notes = TempNotes,
                CreatedAt = DateTime.Now
            };

            _context.BankAccounts.Add(tempAccount);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created temp bank account for CSV import: {AccountNumber} (Id={Id}) - real bank accounts remain untouched.", tempAccount.AccountNumber, tempAccount.Id);
            return tempAccount;
        }
    }
}
