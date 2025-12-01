using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class BankAccountBalanceService : IBankAccountBalanceService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<BankAccountBalanceService> _logger;

        public BankAccountBalanceService(ForexDbContext context, ILogger<BankAccountBalanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BankAccountBalance> GetBankAccountBalanceAsync(int bankAccountId, string currencyCode)
        {
            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.Currency)
                .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            // Get CurrencyId from CurrencyCode (for backward compatibility)
            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => (c.Code ?? "").ToUpperInvariant().Trim() == currencyCode.ToUpperInvariant().Trim());

            // Since each bank account now has only one currency, validate currency match using CurrencyId
            if (currency != null && bankAccount.CurrencyId != currency.Id)
            {
                var bankCurrencyCode = bankAccount.Currency != null ? bankAccount.Currency.Code : bankAccount.CurrencyCode;
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankCurrencyCode}, not {currencyCode}");
            }
            else if (currency == null && bankAccount.CurrencyCode != currencyCode)
            {
                // Fallback to CurrencyCode comparison if currency not found
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankAccount.CurrencyCode}, not {currencyCode}");
            }

            // Return a BankAccountBalance object for compatibility
            return new BankAccountBalance
            {
                BankAccountId = bankAccountId,
                CurrencyId = bankAccount.CurrencyId,
                CurrencyCode = bankAccount.CurrencyCode,
                Balance = bankAccount.AccountBalance,
                LastUpdated = bankAccount.LastModified ?? bankAccount.CreatedAt,
                BankAccount = bankAccount
            };
        }

        public async Task<List<BankAccountBalance>> GetBankAccountBalancesAsync(int bankAccountId)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                return new List<BankAccountBalance>();

            // Return single balance for the account's currency
            return new List<BankAccountBalance>
            {
                new BankAccountBalance
                {
                    BankAccountId = bankAccountId,
                    CurrencyId = bankAccount.CurrencyId,
                    CurrencyCode = bankAccount.CurrencyCode,
                    Balance = bankAccount.AccountBalance,
                    LastUpdated = bankAccount.LastModified ?? bankAccount.CreatedAt,
                    BankAccount = bankAccount
                }
            };
        }

        public async Task<BankAccountBalanceSummary> GetBankAccountBalanceSummaryAsync(int bankAccountId)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            var balances = await GetBankAccountBalancesAsync(bankAccountId);

            // Calculate total balance in IRR (simplified - could use exchange rates)
            var totalInIRR = bankAccount.CurrencyCode == "IRR" ? bankAccount.AccountBalance : 0;

            return new BankAccountBalanceSummary
            {
                BankAccountId = bankAccount.Id,
                BankName = bankAccount.BankName,
                AccountNumber = bankAccount.AccountNumber,
                AccountHolderName = bankAccount.AccountHolderName,
                CurrencyBalances = balances.Where(b => b.Balance != 0).ToList(),
                TotalBalanceInIRR = totalInIRR
            };
        }

        public async Task<List<BankAccountBalanceSummary>> GetAllBankAccountBalanceSummariesAsync()
        {
            var bankAccounts = await _context.BankAccounts
                .Where(b => b.AccountBalance != 0)
                .ToListAsync();

            var summaries = new List<BankAccountBalanceSummary>();
            foreach (var account in bankAccounts)
            {
                var summary = await GetBankAccountBalanceSummaryAsync(account.Id);
                summaries.Add(summary);
            }

            return summaries.OrderBy(s => s.BankName).ThenBy(s => s.AccountNumber).ToList();
        }

        public async Task UpdateBankAccountBalanceAsync(int bankAccountId, string currencyCode, decimal amount, string reason)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            // Validate currency match
            if (bankAccount.CurrencyCode != currencyCode)
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankAccount.CurrencyCode}, not {currencyCode}");

            bankAccount.AccountBalance += amount;
            bankAccount.LastModified = DateTime.Now;
            bankAccount.Notes = $"{reason} - {DateTime.Now:yyyy-MM-dd HH:mm}";

            _context.BankAccounts.Update(bankAccount);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated bank account {BankAccountId} balance in {Currency}: {Amount} ({Reason})",
                bankAccountId, currencyCode, amount, reason);
        }

        public async Task ProcessAccountingDocumentAsync(AccountingDocument document)
        {
            // Handle Payer side of the transaction
            if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
            {
                // System pays from bank account - bank balance DECREASES
                await UpdateBankAccountBalanceAsync(
                    document.PayerBankAccountId.Value,
                    document.CurrencyCode,
                    -document.Amount,
                    $"System Payment - Document #{document.Id} - {document.Title}"
                );
            }

            // Handle Receiver side of the transaction
            if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
            {
                // System receives to bank account - bank balance INCREASES
                await UpdateBankAccountBalanceAsync(
                    document.ReceiverBankAccountId.Value,
                    document.CurrencyCode,
                    document.Amount,
                    $"System Receipt - Document #{document.Id} - {document.Title}"
                );
            }

            _logger.LogInformation("Processed bilateral accounting document {DocumentId} for bank accounts: " +
                "Payer Bank: {PayerBankId}, Receiver Bank: {ReceiverBankId}, Amount: {Amount} {Currency}",
                document.Id,
                document.PayerBankAccountId,
                document.ReceiverBankAccountId,
                document.Amount,
                document.CurrencyCode);
        }

        public async Task SetInitialBalanceAsync(int bankAccountId, string currencyCode, decimal amount, string notes)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            // Validate currency match
            if (bankAccount.CurrencyCode != currencyCode)
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankAccount.CurrencyCode}, not {currencyCode}");

            bankAccount.AccountBalance = amount;
            bankAccount.LastModified = DateTime.Now;
            bankAccount.Notes = $"Initial balance set: {notes}";

            _context.BankAccounts.Update(bankAccount);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Set initial balance for bank account {BankAccountId} in {Currency}: {Amount}",
                bankAccountId, currencyCode, amount);
        }
    }
}
