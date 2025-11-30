using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class CustomerBalanceService : ICustomerBalanceService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CustomerBalanceService> _logger;

        public CustomerBalanceService(ForexDbContext context, ILogger<CustomerBalanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CustomerBalance> GetCustomerBalanceAsync(int customerId, string currencyCode)
        {
            // Get CurrencyId from CurrencyCode
            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => (c.Code ?? "").ToUpperInvariant().Trim() == currencyCode.ToUpperInvariant().Trim());

            CustomerBalance? balance = null;
            if (currency != null)
            {
                // Try to find by CurrencyId first (preferred)
                balance = await _context.CustomerBalances
                    .Include(b => b.Customer)
                    .Include(b => b.Currency)
                    .FirstOrDefaultAsync(b => b.CustomerId == customerId && b.CurrencyId == currency.Id);
            }

            // Fallback to CurrencyCode lookup if not found
            if (balance == null)
            {
                var normalizedCode = currencyCode.ToUpperInvariant().Trim();
                var balances = await _context.CustomerBalances
                    .Include(b => b.Customer)
                    .Include(b => b.Currency)
                    .Where(b => b.CustomerId == customerId)
                    .ToListAsync();
                balance = balances.FirstOrDefault(b => 
                    (b.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCode);
            }

            if (balance == null)
            {
                // Create new balance with zero amount
                balance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyId = currency?.Id,
                    CurrencyCode = currencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.CustomerBalances.Add(balance);
                await _context.SaveChangesAsync();
                
                // Re-query to get navigation properties
                balance = await _context.CustomerBalances
                    .Include(b => b.Customer)
                    .Include(b => b.Currency)
                    .FirstAsync(b => b.Id == balance.Id);
            }
            else if (currency != null && !balance.CurrencyId.HasValue)
            {
                // Ensure CurrencyId is set
                balance.CurrencyId = currency.Id;
                await _context.SaveChangesAsync();
            }

            return balance;
        }

        public async Task<List<CustomerBalance>> GetCustomerBalancesAsync(int customerId)
        {
            return await _context.CustomerBalances
                .Include(b => b.Customer)
                .Where(b => b.CustomerId == customerId)
                .OrderBy(b => b.CurrencyCode)
                .ToListAsync();
        }

        public async Task<CustomerBalanceSummary> GetCustomerBalanceSummaryAsync(int customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                throw new ArgumentException($"Customer with ID {customerId} not found");

            var balances = await GetCustomerBalancesAsync(customerId);

            // Calculate net balance in primary currency (IRR)
            var primaryBalance = balances.FirstOrDefault(b => b.CurrencyCode == "IRR")?.Balance ?? 0;

            return new CustomerBalanceSummary
            {
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CustomerPhone = customer.PhoneNumber ?? "",
                CurrencyBalances = balances.Where(b => b.Balance != 0).ToList(),
                NetBalanceInPrimaryCurrency = primaryBalance,
                PrimaryCurrency = "IRR"
            };
        }

        public async Task<List<CustomerBalanceSummary>> GetAllCustomerBalanceSummariesAsync()
        {
            var customersWithBalances = await _context.CustomerBalances
                .Include(b => b.Customer)
                .Where(b => b.Balance != 0)
                .GroupBy(b => b.CustomerId)
                .Select(g => g.Key)
                .ToListAsync();

            var summaries = new List<CustomerBalanceSummary>();
            foreach (var customerId in customersWithBalances)
            {
                var summary = await GetCustomerBalanceSummaryAsync(customerId);
                summaries.Add(summary);
            }

            return summaries.OrderByDescending(s => Math.Abs(s.NetBalanceInPrimaryCurrency)).ToList();
        }

        public async Task UpdateCustomerBalanceAsync(int customerId, string currencyCode, decimal amount, string reason)
        {
            var balance = await GetCustomerBalanceAsync(customerId, currencyCode);
            
            _logger.LogInformation("BEFORE UPDATE: Customer {CustomerId} {Currency} balance was {OldBalance}", 
                customerId, currencyCode, balance.Balance);
            
            balance.Balance += amount;
            balance.LastUpdated = DateTime.Now;
            balance.Notes = $"{reason} - {DateTime.Now:yyyy-MM-dd HH:mm}";

            _logger.LogInformation("AFTER CALCULATION: Customer {CustomerId} {Currency} balance is now {NewBalance} (added {Amount})", 
                customerId, currencyCode, balance.Balance, amount);

            _context.CustomerBalances.Update(balance);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SAVED TO DB: Updated customer {CustomerId} balance in {Currency}: {Amount} ({Reason})", 
                customerId, currencyCode, amount, reason);
        }

        public async Task ProcessOrderCreationAsync(Order order)
        {
            _logger.LogInformation("Starting ProcessOrderCreationAsync for Order {OrderId}, Customer {CustomerId}", 
                order.Id, order.CustomerId);
            _logger.LogInformation("Order details: Amount={Amount}, Rate={Rate}, TotalAmount={TotalAmount}, FromCurrency={FromCurrency}, ToCurrency={ToCurrency}", 
                order.FromAmount, order.Rate, order.ToAmount, order.FromCurrency?.Code ?? "NULL", order.ToCurrency?.Code ?? "NULL");

            if (order.FromCurrency == null || order.ToCurrency == null)
            {
                _logger.LogError("Order {OrderId} has null currencies: FromCurrency={FromCurrency}, ToCurrency={ToCurrency}", 
                    order.Id, order.FromCurrency?.Code ?? "NULL", order.ToCurrency?.Code ?? "NULL");
                return;
            }

            // Customer pays FromCurrency amount (negative balance)
            await UpdateCustomerBalanceAsync(
                order.CustomerId, 
                order.FromCurrency.Code, 
                -order.FromAmount, 
                $"Order #{order.Id} - Pay {order.FromAmount} {order.FromCurrency.Code}"
            );

            // Customer receives ToCurrency amount (positive balance)
            await UpdateCustomerBalanceAsync(
                order.CustomerId, 
                order.ToCurrency.Code, 
                order.ToAmount, 
                $"Order #{order.Id} - Receive {order.ToAmount} {order.ToCurrency.Code}"
            );

            _logger.LogInformation("Completed ProcessOrderCreationAsync for Order {OrderId} for customer {CustomerId}", 
                order.Id, order.CustomerId);
        }

        public async Task ProcessOrderEditAsync(Order oldOrder, Order newOrder)
        {
            // Reverse old order effects
            await UpdateCustomerBalanceAsync(
                oldOrder.CustomerId, 
                oldOrder.FromCurrency.Code, 
                oldOrder.FromAmount, 
                $"Reverse Order #{oldOrder.Id} edit"
            );

            await UpdateCustomerBalanceAsync(
                oldOrder.CustomerId, 
                oldOrder.ToCurrency.Code, 
                -oldOrder.ToAmount, 
                $"Reverse Order #{oldOrder.Id} edit"
            );

            // Apply new order effects
            await ProcessOrderCreationAsync(newOrder);

            _logger.LogInformation("Processed order edit {OrderId} for customer {CustomerId}", 
                newOrder.Id, newOrder.CustomerId);
        }

        public async Task ProcessAccountingDocumentAsync(AccountingDocument document)
        {
            // Handle Payer side of the transaction
            if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
            {
                // Customer is paying - their debt DECREASES (more positive/less negative)
                await UpdateCustomerBalanceAsync(
                    document.PayerCustomerId.Value,
                    document.CurrencyCode,
                    document.Amount,
                    $"Customer Payment - Document #{document.Id} - {document.Title}"
                );
            }

            // Handle Receiver side of the transaction
            if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
            {
                // Customer is receiving - their debt INCREASES (more negative/less positive)
                await UpdateCustomerBalanceAsync(
                    document.ReceiverCustomerId.Value,
                    document.CurrencyCode,
                    -document.Amount,
                    $"Customer Receipt - Document #{document.Id} - {document.Title}"
                );
            }

            _logger.LogInformation("Processed bilateral accounting document {DocumentId}: " +
                "Payer: {PayerType} {PayerId}, Receiver: {ReceiverType} {ReceiverId}, Amount: {Amount} {Currency}",
                document.Id,
                document.PayerType,
                document.PayerType == PayerType.Customer ? document.PayerCustomerId?.ToString() : document.PayerBankAccountId?.ToString(),
                document.ReceiverType,
                document.ReceiverType == ReceiverType.Customer ? document.ReceiverCustomerId?.ToString() : document.ReceiverBankAccountId?.ToString(),
                document.Amount,
                document.CurrencyCode);
        }

        public async Task SetInitialBalanceAsync(int customerId, string currencyCode, decimal amount, string notes)
        {
            var balance = await GetCustomerBalanceAsync(customerId, currencyCode);
            
            balance.Balance = amount;
            balance.LastUpdated = DateTime.Now;
            balance.Notes = $"Initial balance set: {notes}";

            _context.CustomerBalances.Update(balance);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Set initial balance for customer {CustomerId} in {Currency}: {Amount}", 
                customerId, currencyCode, amount);
        }
    }
}
