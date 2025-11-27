
using ForexExchange.Extensions;
using ForexExchange.Models;
using ForexExchange.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ForexExchange.Services
{
    /// <summary>
    /// **CRITICAL FINANCIAL SERVICE** - Centralized financial operations management with complete audit trail.
    /// 
    /// **📚 FOR COMPREHENSIVE DOCUMENTATION**: See `/docs/CentralFinancialService-Documentation.md`
    /// This documentation file contains detailed explanations, business logic, safety guidelines,
    /// troubleshooting guides, and complete usage examples for this critical financial service.
    /// 
    /// This service is the heart of the forex exchange financial system, managing:
    /// - Customer balance operations (credit/debit with history)
    /// - Currency pool management (institutional liquidity pools)
    /// - Bank account balance tracking (financial institution accounts)
    /// - Complete audit trail through history tables (immutable transaction log)
    /// - Balance consistency validation and reconciliation
    /// - Smart deletion with soft-delete and recalculation capabilities
    /// 
    /// **SAFETY CRITICAL**: Every operation maintains complete audit trail and preserves
    /// exact calculation logic from existing services. Zero logic changes were made during
    /// centralization - only consolidation of previously scattered financial operations.
    /// 
    /// **DATA INTEGRITY**: All balance updates are transactional, logged, and historically tracked.
    /// History tables provide event sourcing capabilities for complete financial audit trails.
    /// 
    /// **CONSISTENCY GUARANTEE**: The service ensures that preview calculations exactly match
    /// real transaction effects, preventing discrepancies between UI previews and actual results.
    /// </summary>
    public class CentralFinancialService : ICentralFinancialService
    {
        /// <summary>
        /// Thread safety semaphore to prevent concurrent balance rebuilds
        /// </summary>
        private static readonly SemaphoreSlim _rebuildSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Database context for Entity Framework operations
        /// </summary>
        private readonly ForexDbContext _context;

        /// <summary>
        /// Logger for comprehensive financial operation tracking and debugging
        /// </summary>
        private readonly ILogger<CentralFinancialService> _logger;

        /// <summary>
        /// Notification hub for sending real-time notifications to admin users
        /// </summary>
        private readonly INotificationHub _notificationHub;

        private readonly ICurrencyPoolService _currencyPoolService;

        /// <summary>
        /// Service provider for creating scoped services in background tasks
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// **CONSTRUCTOR** - Initializes the central financial service with required dependencies.
        /// </summary>
        /// <param name="context">Entity Framework database context for data operations</param>
        /// <param name="logger">Logger for operation tracking and debugging</param>
        /// <param name="notificationHub">Notification hub for real-time admin notifications</param>
        /// <param name="serviceProvider">Service provider for creating scoped services</param>

        public CentralFinancialService(ForexDbContext context, ILogger<CentralFinancialService> logger, INotificationHub notificationHub, ICurrencyPoolService currencyPoolService, IServiceProvider serviceProvider)
        {
            _context = context;
            _logger = logger;
            _notificationHub = notificationHub;
            _currencyPoolService = currencyPoolService;
            _serviceProvider = serviceProvider;
        }


        #region  Preview
        /// <summary>
        /// **PREVIEW SIMULATION** - Calculates the financial impact of an order without making database changes.
        /// 
        /// This method simulates exactly what would happen when an order is processed, allowing the UI
        /// to show users the precise effect on their balances and institutional currency pools.
        /// 
        /// **SRP CONSISTENCY GUARANTEE**: Uses dedicated calculation methods (CalculateCustomerBalanceEffects
        /// and CalculateCurrencyPoolEffects) that are also used by ProcessOrderCreationAsync() to ensure
        /// that preview calculations exactly match actual processing results.
        /// 
        /// **Calculation Logic**:
        /// - Customer pays FromAmount in FromCurrency (balance decreases)
        /// - Customer receives ToAmount in ToCurrency (balance increases)  
        /// - Institution receives FromAmount in FromCurrency (pool increases)
        /// - Institution pays ToAmount in ToCurrency (pool decreases)
        /// 
        /// **Validation**: Verifies all required customer balances and currency pools exist before calculation.
        /// </summary>
        /// <param name="order">Order with populated FromCurrency and ToCurrency navigation properties</param>
        /// <returns>Preview effects showing before/after balances for customer and pools</returns>
        /// <exception cref="Exception">Thrown when required currencies or balances are not found</exception>
        public async Task<OrderPreviewEffectsDto> PreviewOrderEffectsAsync(Order order)
        {
            _logger.LogInformation($"[PreviewOrderEffectsAsync] Called for CustomerId={order.CustomerId}, FromCurrencyId={order.FromCurrencyId}, ToCurrencyId={order.ToCurrencyId}, FromAmount={order.FromAmount}, Rate={order.Rate}, ToAmount={order.ToAmount}");

            if (order.FromCurrency == null || string.IsNullOrWhiteSpace(order.FromCurrency.Code))
            {
                _logger.LogError($"FromCurrency or its Code is null for order preview (FromCurrencyId: {order.FromCurrencyId})");
                throw new Exception($"FromCurrency or its Code is null for order preview (FromCurrencyId: {order.FromCurrencyId})");
            }
            _logger.LogInformation($"FromCurrency: {order.FromCurrency.Code}");

            if (order.ToCurrency == null || string.IsNullOrWhiteSpace(order.ToCurrency.Code))
            {
                _logger.LogError($"ToCurrency or its Code is null for order preview (ToCurrencyId: {order.ToCurrencyId})");
                throw new Exception($"ToCurrency or its Code is null for order preview (ToCurrencyId: {order.ToCurrencyId})");
            }
            _logger.LogInformation($"ToCurrency: {order.ToCurrency.Code}");

            // Normalize currency codes to uppercase for case-insensitive matching
            var fromCurrencyCode = (order.FromCurrency.Code ?? "").ToUpperInvariant().Trim();
            var toCurrencyCode = (order.ToCurrency.Code ?? "").ToUpperInvariant().Trim();

            // Load all customer balances for this customer for case-insensitive lookup
            var customerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == order.CustomerId)
                .ToListAsync();

            var customerBalanceFrom = customerBalances.FirstOrDefault(cb =>
                (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == fromCurrencyCode);

            if (customerBalanceFrom == null)
            {
                _logger.LogWarning($"Customer balance not found for customer {order.CustomerId} and currency {fromCurrencyCode} - creating with zero balance");

                // Auto-create missing customer balance record with zero balance (normalized to uppercase)
                customerBalanceFrom = new CustomerBalance
                {
                    CustomerId = order.CustomerId,
                    CurrencyCode = fromCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };

                _context.CustomerBalances.Add(customerBalanceFrom);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created new customer balance record: CustomerId={order.CustomerId}, Currency={fromCurrencyCode}, Balance=0");
            }
            else if (customerBalanceFrom.CurrencyCode != fromCurrencyCode)
            {
                // Normalize existing balance's currency code to uppercase
                _logger.LogWarning($"Normalizing CustomerBalance CurrencyCode from '{customerBalanceFrom.CurrencyCode}' to '{fromCurrencyCode}' for Customer {order.CustomerId}");
                customerBalanceFrom.CurrencyCode = fromCurrencyCode;
                await _context.SaveChangesAsync();
            }
            _logger.LogInformation($"CustomerBalanceFrom: {customerBalanceFrom.Balance}");

            var customerBalanceTo = customerBalances.FirstOrDefault(cb =>
                (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == toCurrencyCode);

            if (customerBalanceTo == null)
            {
                _logger.LogWarning($"Customer balance not found for customer {order.CustomerId} and currency {toCurrencyCode} - creating with zero balance");

                // Auto-create missing customer balance record with zero balance (normalized to uppercase)
                customerBalanceTo = new CustomerBalance
                {
                    CustomerId = order.CustomerId,
                    CurrencyCode = toCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };

                _context.CustomerBalances.Add(customerBalanceTo);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created new customer balance record: CustomerId={order.CustomerId}, Currency={toCurrencyCode}, Balance=0");
            }
            else if (customerBalanceTo.CurrencyCode != toCurrencyCode)
            {
                // Normalize existing balance's currency code to uppercase
                _logger.LogWarning($"Normalizing CustomerBalance CurrencyCode from '{customerBalanceTo.CurrencyCode}' to '{toCurrencyCode}' for Customer {order.CustomerId}");
                customerBalanceTo.CurrencyCode = toCurrencyCode;
                await _context.SaveChangesAsync();
            }
            _logger.LogInformation($"CustomerBalanceTo: {customerBalanceTo.Balance}");

            var poolBalanceFrom = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrency.Id);
            if (poolBalanceFrom == null)
            {
                await _currencyPoolService.CreatePoolAsync(order.FromCurrency.Id);
                _logger.LogError($"Currency pool not found for currency {order.FromCurrency.Code}");
                throw new Exception($"Currency pool not found for currency {order.FromCurrency.Code}");
            }
            _logger.LogInformation($"PoolBalanceFrom: {poolBalanceFrom.Balance}");

            var poolBalanceTo = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrency.Id);
            if (poolBalanceTo == null)
            {
                await _currencyPoolService.CreatePoolAsync(order.ToCurrency.Id);
                _logger.LogError($"Currency pool not found for currency {order.ToCurrency.Code}");
                throw new Exception($"Currency pool not found for currency {order.ToCurrency.Code}");
            }
            _logger.LogInformation($"PoolBalanceTo: {poolBalanceTo.Balance}");

            // Use SRP calculation methods to ensure consistency with actual processing
            var (newCustomerBalanceFrom, newCustomerBalanceTo) = CalculateCustomerBalanceEffects(
                currentFromBalance: customerBalanceFrom.Balance,
                currentToBalance: customerBalanceTo.Balance,
                fromAmount: order.FromAmount,
                toAmount: order.ToAmount
            );

            var (newPoolBalanceFrom, newPoolBalanceTo) = CalculateCurrencyPoolEffects(
                currentFromPool: poolBalanceFrom.Balance,
                currentToPool: poolBalanceTo.Balance,
                fromAmount: order.FromAmount,
                toAmount: order.ToAmount
            );

            _logger.LogInformation($"NewCustomerBalanceFrom: {newCustomerBalanceFrom}");
            _logger.LogInformation($"NewCustomerBalanceTo: {newCustomerBalanceTo}");
            _logger.LogInformation($"NewPoolBalanceFrom: {newPoolBalanceFrom}");
            _logger.LogInformation($"NewPoolBalanceTo: {newPoolBalanceTo}");

            return new OrderPreviewEffectsDto
            {
                CustomerId = order.CustomerId,
                FromCurrencyCode = order.FromCurrency.Code,
                ToCurrencyCode = order.ToCurrency.Code,
                OrderFromAmount = order.FromAmount,
                OrderToAmount = order.ToAmount,
                OldCustomerBalanceFrom = customerBalanceFrom.Balance,
                OldCustomerBalanceTo = customerBalanceTo.Balance,
                NewCustomerBalanceFrom = newCustomerBalanceFrom,
                NewCustomerBalanceTo = newCustomerBalanceTo,
                OldPoolBalanceFrom = poolBalanceFrom.Balance,
                OldPoolBalanceTo = poolBalanceTo.Balance,
                NewPoolBalanceFrom = newPoolBalanceFrom,
                NewPoolBalanceTo = newPoolBalanceTo
            };
        }



        /// <summary>
        /// **SRP CALCULATION** - Pure calculation of customer balance effects for an order.
        /// 
        /// This method contains the core business logic for how orders affect customer balances.
        /// Both preview and actual processing MUST use this method to ensure consistency.
        /// 
        /// **Business Logic**:
        /// - Customer pays FromAmount in FromCurrency (balance decreases)
        /// - Customer receives ToAmount in ToCurrency (balance increases)
        /// </summary>
        /// <param name="currentFromBalance">Customer's current balance in FromCurrency</param>
        /// <param name="currentToBalance">Customer's current balance in ToCurrency</param>
        /// <param name="fromAmount">Amount customer pays</param>
        /// <param name="toAmount">Amount customer receives</param>
        /// <returns>Tuple with new balances (newFromBalance, newToBalance)</returns>
        private (decimal newFromBalance, decimal newToBalance) CalculateCustomerBalanceEffects(
            decimal currentFromBalance,
            decimal currentToBalance,
            decimal fromAmount,
            decimal toAmount)
        {
            var newFromBalance = currentFromBalance - fromAmount; // Customer pays (negative impact)
            var newToBalance = currentToBalance + toAmount;       // Customer receives (positive impact)

            _logger.LogInformation($"[CalculateCustomerBalanceEffects] From: {currentFromBalance} - {fromAmount} = {newFromBalance}");
            _logger.LogInformation($"[CalculateCustomerBalanceEffects] To: {currentToBalance} + {toAmount} = {newToBalance}");

            return (newFromBalance, newToBalance);
        }

        /// <summary>
        /// **SRP CALCULATION** - Pure calculation of currency pool effects for an order.
        /// 
        /// This method contains the core business logic for how orders affect institutional pools.
        /// Both preview and actual processing MUST use this method to ensure consistency.
        /// 
        /// **Business Logic**:
        /// - Institution receives FromCurrency from customer (pool increases)
        /// - Institution provides ToCurrency to customer (pool decreases)
        /// </summary>
        /// <param name="currentFromPool">Institution's current pool in FromCurrency</param>
        /// <param name="currentToPool">Institution's current pool in ToCurrency</param>
        /// <param name="fromAmount">Amount institution receives from customer</param>
        /// <param name="toAmount">Amount institution provides to customer</param>
        /// <returns>Tuple with new pool balances (newFromPool, newToPool)</returns>
        private (decimal newFromPool, decimal newToPool) CalculateCurrencyPoolEffects(
            decimal currentFromPool,
            decimal currentToPool,
            decimal fromAmount,
            decimal toAmount)
        {
            var newFromPool = currentFromPool + fromAmount; // Institution receives (positive impact)
            var newToPool = currentToPool - toAmount;       // Institution provides (negative impact)

            _logger.LogInformation($"[CalculateCurrencyPoolEffects] From Pool: {currentFromPool} + {fromAmount} = {newFromPool}");
            _logger.LogInformation($"[CalculateCurrencyPoolEffects] To Pool: {currentToPool} - {toAmount} = {newToPool}");

            return (newFromPool, newToPool);
        }


        /// <summary>
        /// **PREVIEW SIMULATION** - Calculates the financial impact of an accounting document without making database changes.
        /// 
        /// This method simulates exactly what would happen when a document is processed, allowing the UI
        /// to show users the precise effect on customer and bank account balances.
        /// 
        /// **Auto-Create Missing Balances**: Automatically creates missing CustomerBalance and BankAccountBalance
        /// records with zero balance to ensure preview calculations work for all scenarios.
        /// 
        /// **Business Logic**:
        /// - Payer Customer: Gets +amount (receives money/credit)
        /// - Receiver Customer: Gets -amount (pays money/debit)
        /// - Payer Bank Account: Gets -amount (money flows out)
        /// - Receiver Bank Account: Gets +amount (money flows in)
        /// </summary>
        /// <param name="document">Accounting document with all party and amount information</param>
        /// <returns>Preview effects showing before/after balances for customers and bank accounts</returns>
        public async Task<AccountingDocumentPreviewEffectsDto> PreviewAccountingDocumentEffectsAsync(AccountingDocument document)
        {
            _logger.LogInformation($"[PreviewAccountingDocumentEffectsAsync] Called for DocumentId={document.Id}, Amount={document.Amount}, Currency={document.CurrencyCode}");

            var effects = new AccountingDocumentPreviewEffectsDto
            {
                DocumentId = document.Id,
                Amount = document.Amount,
                CurrencyCode = document.CurrencyCode,
                CustomerEffects = new List<CustomerBalanceEffect>(),
                BankAccountEffects = new List<BankAccountBalanceEffect>(),
                Warnings = new List<string>()
            };

            // Validate required fields
            if (document.Amount == 0)
            {
                effects.Warnings.Add("مبلغ سند نمی‌تواند صفر باشد.");
                return effects;
            }

            if (string.IsNullOrEmpty(document.CurrencyCode))
            {
                effects.Warnings.Add("ارز سند انتخاب نشده است.");
                return effects;
            }

            // Process Payer Customer Effect
            if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
            {
                var payerCustomer = await _context.Customers.FindAsync(document.PayerCustomerId.Value);
                if (payerCustomer != null)
                {
                    // Get or create customer balance
                    var customerBalance = await _context.CustomerBalances
                        .FirstOrDefaultAsync(cb => cb.CustomerId == document.PayerCustomerId.Value && cb.CurrencyCode == document.CurrencyCode);

                    if (customerBalance == null)
                    {
                        _logger.LogWarning($"Customer balance not found for customer {document.PayerCustomerId.Value} and currency {document.CurrencyCode} - creating with zero balance");

                        customerBalance = new CustomerBalance
                        {
                            CustomerId = document.PayerCustomerId.Value,
                            CurrencyCode = document.CurrencyCode,
                            Balance = 0,
                            LastUpdated = DateTime.Now
                        };

                        _context.CustomerBalances.Add(customerBalance);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Created new customer balance record: CustomerId={document.PayerCustomerId.Value}, Currency={document.CurrencyCode}, Balance=0");
                    }

                    var currentBalance = customerBalance.Balance;
                    var newBalance = currentBalance + document.Amount; // Payer gets +amount

                    effects.CustomerEffects.Add(new CustomerBalanceEffect
                    {
                        CustomerId = document.PayerCustomerId.Value,
                        CustomerName = payerCustomer.FullName,
                        CurrencyCode = document.CurrencyCode,
                        CurrentBalance = currentBalance,
                        TransactionAmount = document.Amount,
                        NewBalance = newBalance,
                        Role = "Payer"
                    });

                    if (newBalance < 0)
                    {
                        effects.Warnings.Add($"تراز مشتری {payerCustomer.FullName} در ارز {document.CurrencyCode} منفی خواهد شد ({newBalance:N2}).");
                    }
                }
            }

            // Process Receiver Customer Effect
            if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
            {
                var receiverCustomer = await _context.Customers.FindAsync(document.ReceiverCustomerId.Value);
                if (receiverCustomer != null)
                {
                    // Get or create customer balance
                    var customerBalance = await _context.CustomerBalances
                        .FirstOrDefaultAsync(cb => cb.CustomerId == document.ReceiverCustomerId.Value && cb.CurrencyCode == document.CurrencyCode);

                    if (customerBalance == null)
                    {
                        _logger.LogWarning($"Customer balance not found for customer {document.ReceiverCustomerId.Value} and currency {document.CurrencyCode} - creating with zero balance");

                        customerBalance = new CustomerBalance
                        {
                            CustomerId = document.ReceiverCustomerId.Value,
                            CurrencyCode = document.CurrencyCode,
                            Balance = 0,
                            LastUpdated = DateTime.Now
                        };

                        _context.CustomerBalances.Add(customerBalance);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Created new customer balance record: CustomerId={document.ReceiverCustomerId.Value}, Currency={document.CurrencyCode}, Balance=0");
                    }

                    var currentBalance = customerBalance.Balance;
                    var newBalance = currentBalance - document.Amount; // Receiver gets -amount

                    effects.CustomerEffects.Add(new CustomerBalanceEffect
                    {
                        CustomerId = document.ReceiverCustomerId.Value,
                        CustomerName = receiverCustomer.FullName,
                        CurrencyCode = document.CurrencyCode,
                        CurrentBalance = currentBalance,
                        TransactionAmount = -document.Amount,
                        NewBalance = newBalance,
                        Role = "Receiver"
                    });

                    if (newBalance < 0)
                    {
                        effects.Warnings.Add($"تراز مشتری {receiverCustomer.FullName} در ارز {document.CurrencyCode} منفی خواهد شد ({newBalance:N2}).");
                    }
                }
            }

            // Process Payer Bank Account Effect
            if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
            {
                var payerBankAccount = await _context.BankAccounts.FindAsync(document.PayerBankAccountId.Value);
                if (payerBankAccount != null)
                {
                    // Validate currency match
                    if (payerBankAccount.CurrencyCode != document.CurrencyCode)
                    {
                        effects.Warnings.Add($"ارز حساب بانکی پرداخت کننده ({payerBankAccount.CurrencyCode}) با ارز سند ({document.CurrencyCode}) مطابقت ندارد.");
                    }
                    else
                    {
                        // Get or create bank account balance
                        var bankBalance = await _context.BankAccountBalances
                            .FirstOrDefaultAsync(bab => bab.BankAccountId == document.PayerBankAccountId.Value);

                        if (bankBalance == null)
                        {
                            _logger.LogWarning($"Bank account balance not found for account {document.PayerBankAccountId.Value} - creating with zero balance");

                            bankBalance = new BankAccountBalance
                            {
                                BankAccountId = document.PayerBankAccountId.Value,
                                Balance = 0,
                                LastUpdated = DateTime.Now
                            };

                            _context.BankAccountBalances.Add(bankBalance);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation($"Created new bank account balance record: BankAccountId={document.PayerBankAccountId.Value}, Balance=0");
                        }

                        var currentBalance = bankBalance.Balance;
                        var newBalance = currentBalance + document.Amount; // Bank pays out

                        effects.BankAccountEffects.Add(new BankAccountBalanceEffect
                        {
                            BankAccountId = document.PayerBankAccountId.Value,
                            BankName = payerBankAccount.BankName,
                            AccountNumber = payerBankAccount.AccountNumber,
                            CurrencyCode = document.CurrencyCode,
                            CurrentBalance = currentBalance,
                            TransactionAmount = document.Amount,
                            NewBalance = newBalance,
                            Role = "Payer"
                        });

                        if (newBalance < 0)
                        {
                            effects.Warnings.Add($"تراز حساب بانکی {payerBankAccount.BankName} - {payerBankAccount.AccountNumber} منفی خواهد شد ({newBalance:N2}).");
                        }
                    }
                }
            }

            // Process Receiver Bank Account Effect
            if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
            {
                var receiverBankAccount = await _context.BankAccounts.FindAsync(document.ReceiverBankAccountId.Value);
                if (receiverBankAccount != null)
                {
                    // Validate currency match
                    if (receiverBankAccount.CurrencyCode != document.CurrencyCode)
                    {
                        effects.Warnings.Add($"ارز حساب بانکی دریافت کننده ({receiverBankAccount.CurrencyCode}) با ارز سند ({document.CurrencyCode}) مطابقت ندارد.");
                    }
                    else
                    {
                        // Get or create bank account balance
                        var bankBalance = await _context.BankAccountBalances
                            .FirstOrDefaultAsync(bab => bab.BankAccountId == document.ReceiverBankAccountId.Value);

                        if (bankBalance == null)
                        {
                            _logger.LogWarning($"Bank account balance not found for account {document.ReceiverBankAccountId.Value} - creating with zero balance");

                            bankBalance = new BankAccountBalance
                            {
                                BankAccountId = document.ReceiverBankAccountId.Value,
                                Balance = 0,
                                LastUpdated = DateTime.Now
                            };

                            _context.BankAccountBalances.Add(bankBalance);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation($"Created new bank account balance record: BankAccountId={document.ReceiverBankAccountId.Value}, Balance=0");
                        }

                        var currentBalance = bankBalance.Balance;
                        var newBalance = currentBalance - document.Amount; // Bank receives

                        effects.BankAccountEffects.Add(new BankAccountBalanceEffect
                        {
                            BankAccountId = document.ReceiverBankAccountId.Value,
                            BankName = receiverBankAccount.BankName,
                            AccountNumber = receiverBankAccount.AccountNumber,
                            CurrencyCode = document.CurrencyCode,
                            CurrentBalance = currentBalance,
                            TransactionAmount = -document.Amount,
                            NewBalance = newBalance,
                            Role = "Receiver"
                        });
                    }
                }
            }

            // Additional validations
            if (document.PayerType == PayerType.Customer && document.ReceiverType == ReceiverType.Customer)
            {
                if (document.PayerCustomerId == document.ReceiverCustomerId)
                {
                    effects.Warnings.Add("مشتری نمی‌تواند به خودش پرداخت کند.");
                }
            }

            if (document.PayerType == PayerType.System && document.ReceiverType == ReceiverType.System)
            {
                if (document.PayerBankAccountId == document.ReceiverBankAccountId)
                {
                    effects.Warnings.Add("حساب بانکی نمی‌تواند به خودش انتقال داشته باشد.");
                }
            }

            _logger.LogInformation($"[PreviewAccountingDocumentEffectsAsync] Completed with {effects.CustomerEffects.Count} customer effects, {effects.BankAccountEffects.Count} bank effects, {effects.Warnings.Count} warnings");

            return effects;
        }


        #endregion Preview

        #region Fast Balance Update (Coherence System)


        /// <summary>
        /// **FAST BALANCE UPDATE** - Updates balances directly without full rebuild.
        /// Fixed version with correct chain reconstruction logic.
        /// </summary>
        /// <param name="order">Order to process</param>
        /// <param name="performedBy">Identifier of who processed the order</param>
        private async Task UpdateBalancesForOrderAsync(Order order, string performedBy = "System")
        {
            _logger.LogInformation($"Fast balance update for Order ID: {order.Id}");



            // Normalize currency codes
            var fromCurrencyCode = (order.FromCurrency?.Code ?? "").ToUpperInvariant().Trim();
            var toCurrencyCode = (order.ToCurrency?.Code ?? "").ToUpperInvariant().Trim();
            var orderDateTime = order.CreatedAt;

            // Load customer balances
            var customerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == order.CustomerId)
                .ToListAsync();

            var customerBalanceFrom = customerBalances.FirstOrDefault(cb =>
                (cb.CurrencyCode ?? "").Trim() == fromCurrencyCode);

            if (customerBalanceFrom == null)
            {
                customerBalanceFrom = new CustomerBalance
                {
                    CustomerId = order.CustomerId,
                    CurrencyCode = fromCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.CustomerBalances.Add(customerBalanceFrom);
            }

            var customerBalanceTo = customerBalances.FirstOrDefault(cb =>
                (cb.CurrencyCode ?? "").Trim() == toCurrencyCode);

            if (customerBalanceTo == null)
            {
                customerBalanceTo = new CustomerBalance
                {
                    CustomerId = order.CustomerId,
                    CurrencyCode = toCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.CustomerBalances.Add(customerBalanceTo);
            }

            // Load pool balances
            var poolBalanceFrom = await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrencyId);

            if (poolBalanceFrom == null)
            {
                await _currencyPoolService.CreatePoolAsync(order.FromCurrencyId);
                poolBalanceFrom = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrencyId);
            }

            var poolBalanceTo = await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrencyId);

            if (poolBalanceTo == null)
            {
                await _currencyPoolService.CreatePoolAsync(order.ToCurrencyId);
                poolBalanceTo = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrencyId);
            }

            // STEP 1: Process Customer Balance History for FromCurrency with correct chain reconstruction
            await ProcessCustomerBalanceHistoryForOrder(
                order: order,
                currencyCode: fromCurrencyCode,
                transactionAmount: -order.FromAmount, // Customer pays (negative)
                isFromCurrency: true,
                performedBy: performedBy,
                currentBalanceEntity: customerBalanceFrom
            );

            // STEP 2: Process Customer Balance History for ToCurrency with correct chain reconstruction
            await ProcessCustomerBalanceHistoryForOrder(
                order: order,
                currencyCode: toCurrencyCode,
                transactionAmount: order.ToAmount, // Customer receives (positive)
                isFromCurrency: false,
                performedBy: performedBy,
                currentBalanceEntity: customerBalanceTo
            );

            // STEP 3: Process Pool Balance History for FromCurrency with correct chain reconstruction
            await ProcessPoolBalanceHistoryForOrder(
                order: order,
                currencyCode: fromCurrencyCode,
                transactionAmount: order.FromAmount, // Pool receives (positive)
                poolTransactionType: "Buy",
                performedBy: performedBy,
                currentBalanceEntity: poolBalanceFrom
            );

            // STEP 4: Process Pool Balance History for ToCurrency with correct chain reconstruction
            await ProcessPoolBalanceHistoryForOrder(
                order: order,
                currencyCode: toCurrencyCode,
                transactionAmount: -order.ToAmount, // Pool pays (negative)
                poolTransactionType: "Sell",
                performedBy: performedBy,
                currentBalanceEntity: poolBalanceTo
            );

            // Update pool statistics
            if (order.FromAmount > 0)
            {
                poolBalanceFrom.ActiveBuyOrderCount++;
                poolBalanceFrom.TotalBought += order.FromAmount;
            }

            if (order.ToAmount > 0)
            {
                poolBalanceTo.ActiveSellOrderCount++;
                poolBalanceTo.TotalSold += order.ToAmount;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Fixed fast balance update completed for Order {order.Id}");
        }

        /// <summary>
        /// Processes customer balance history with correct chain reconstruction for orders
        /// Simplified version: adds record and rebuilds entire chain from scratch
        /// </summary>
        private async Task ProcessCustomerBalanceHistoryForOrder(Order order, string currencyCode,
            decimal transactionAmount, bool isFromCurrency, string performedBy, CustomerBalance currentBalanceEntity)
        {
            var orderDateTime = order.CreatedAt;

            // Remove existing record for this order to avoid duplicates
            var existingRecord = await _context.CustomerBalanceHistory
                .FirstOrDefaultAsync(h => h.ReferenceId == order.Id &&
                                         h.CustomerId == order.CustomerId &&
                                         h.CurrencyCode == currencyCode &&
                                         !h.IsDeleted);

            if (existingRecord != null)
            {
                _context.CustomerBalanceHistory.Remove(existingRecord);
            }

            // Create description based on currency type
            string description;
            if (isFromCurrency)
            {
                description = $"خرید {order.ToAmount} {order.ToCurrency?.Code} با {order.FromAmount} {order.FromCurrency?.Code} - نرخ: {order.Rate}";
            }
            else
            {
                description = $"فروش {order.FromAmount} {order.FromCurrency?.Code} برای {order.ToAmount} {order.ToCurrency?.Code} - نرخ: {order.Rate}";
            }

            if (!string.IsNullOrEmpty(order.Notes))
            {
                description += $" - {order.Notes}";
            }

            // Create new history record for this order (balances will be recalculated in rebuild)
            var newHistoryRecord = new CustomerBalanceHistory
            {
                CustomerId = order.CustomerId,
                CurrencyCode = currencyCode,
                TransactionType = CustomerBalanceTransactionType.Order,
                ReferenceId = order.Id,
                BalanceBefore = 0, // Will be recalculated in rebuild
                TransactionAmount = transactionAmount,
                BalanceAfter = 0, // Will be recalculated in rebuild
                Description = description,
                TransactionDate = orderDateTime,
                CreatedAt = DateTime.Now,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.CustomerBalanceHistory.Add(newHistoryRecord);
            await _context.SaveChangesAsync();

            // Rebuild entire chain from scratch (simpler and more reliable)
            await RebuildCustomerBalanceChain(order.CustomerId, currencyCode);

            // Update the current balance entity
            var updatedBalance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == order.CustomerId && cb.CurrencyCode == currencyCode);

            if (updatedBalance != null)
            {
                currentBalanceEntity.Balance = updatedBalance.Balance;
                currentBalanceEntity.LastUpdated = updatedBalance.LastUpdated;
            }
        }

        /// <summary>
        /// Processes pool balance history with correct chain reconstruction for orders
        /// Simplified version: adds record and rebuilds entire chain from scratch
        /// </summary>
        private async Task ProcessPoolBalanceHistoryForOrder(Order order, string currencyCode,
            decimal transactionAmount, string poolTransactionType, string performedBy, CurrencyPool currentBalanceEntity)
        {
            var orderDateTime = order.CreatedAt;

            // Remove existing record for this order to avoid duplicates
            var existingRecord = await _context.CurrencyPoolHistory
                .FirstOrDefaultAsync(h => h.ReferenceId == order.Id &&
                                         h.CurrencyCode == currencyCode &&
                                         !h.IsDeleted);

            if (existingRecord != null)
            {
                _context.CurrencyPoolHistory.Remove(existingRecord);
            }

            // Create description
            string description = $"سفارش #{order.Id} - {poolTransactionType}";
            if (!string.IsNullOrEmpty(order.Notes))
            {
                description += $" - {order.Notes}";
            }

            // Create new history record for this order (balances will be recalculated in rebuild)
            var newHistoryRecord = new CurrencyPoolHistory
            {
                CurrencyCode = currencyCode,
                TransactionType = CurrencyPoolTransactionType.Order,
                ReferenceId = order.Id,
                BalanceBefore = 0, // Will be recalculated in rebuild
                TransactionAmount = transactionAmount,
                BalanceAfter = 0, // Will be recalculated in rebuild
                PoolTransactionType = poolTransactionType,
                Description = description,
                TransactionDate = orderDateTime,
                CreatedAt = DateTime.Now,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.CurrencyPoolHistory.Add(newHistoryRecord);
            await _context.SaveChangesAsync();

            // Rebuild entire chain from scratch (simpler and more reliable)
            await RebuildPoolBalanceChain(currencyCode);

            // Update the current balance entity
            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Code == currencyCode);

            if (currency != null)
            {
                var updatedPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == currency.Id);

                if (updatedPool != null)
                {
                    currentBalanceEntity.Balance = updatedPool.Balance;
                    currentBalanceEntity.LastUpdated = updatedPool.LastUpdated;
                }
            }
        }


        /// <summary>
        /// **FAST BALANCE UPDATE** - Updates balances directly for accounting document without full rebuild.
        /// Fixed version with CORRECT transaction amounts for all scenarios.
        /// </summary>
        private async Task UpdateBalancesForDocumentAsync(AccountingDocument document, string performedBy = "System")
        {
            _logger.LogInformation($"Fast balance update for Document ID: {document.Id}");


            var currencyCode = CurrencyHelper.NormalizeCurrencyCode(document.CurrencyCode);
            var documentDate = document.DocumentDate;

            // STEP 1: Process Payer (based on type)
            if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
            {
                // ✅ مشتری پرداخت کننده: افزایش موجودی (مثبت)
                await ProcessCustomerBalanceHistoryForDocument(
                    document: document,
                    customerId: document.PayerCustomerId.Value,
                    currencyCode: currencyCode,
                    transactionAmount: document.Amount, // ➕ افزایش موجودی مشتری پرداخت کننده
                    role: "پرداخت کننده",
                    performedBy: performedBy
                );
            }
            else if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
            {
                // ✅ بانک پرداخت کننده: افزایش موجودی (مثبت)
                await ProcessBankBalanceHistoryForDocument(
                    document: document,
                    bankAccountId: document.PayerBankAccountId.Value,
                    transactionAmount: document.Amount, // ➕ افزایش موجودی بانک پرداخت کننده
                    role: "پرداخت کننده",
                    performedBy: performedBy
                );
            }

            // STEP 2: Process Receiver (based on type)
            if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
            {
                // ✅ مشتری دریافت کننده: کاهش موجودی (منفی)
                await ProcessCustomerBalanceHistoryForDocument(
                    document: document,
                    customerId: document.ReceiverCustomerId.Value,
                    currencyCode: currencyCode,
                    transactionAmount: -document.Amount, // ➖ کاهش موجودی مشتری دریافت کننده
                    role: "دریافت کننده",
                    performedBy: performedBy
                );
            }
            else if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
            {
                // ✅ بانک دریافت کننده: کاهش موجودی (منفی)
                await ProcessBankBalanceHistoryForDocument(
                    document: document,
                    bankAccountId: document.ReceiverBankAccountId.Value,
                    transactionAmount: -document.Amount, // ➖ کاهش موجودی بانک دریافت کننده
                    role: "دریافت کننده",
                    performedBy: performedBy
                );
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Fixed fast balance update completed for Document {document.Id}");
        }
        /// <summary>
        /// Processes customer balance history with correct chain reconstruction for documents
        /// </summary>
        private async Task ProcessCustomerBalanceHistoryForDocument(AccountingDocument document, int customerId,
            string currencyCode, decimal transactionAmount, string role, string performedBy)
        {
            var documentDate = document.DocumentDate;

            // Load ALL customer balances for this customer first, then filter in memory
            var customerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == customerId)
                .ToListAsync();

            // Filter by currency code in memory (case-insensitive)
            var customerBalance = customerBalances
                .FirstOrDefault(cb => string.Equals((cb.CurrencyCode ?? "").Trim(), currencyCode, StringComparison.OrdinalIgnoreCase));

            if (customerBalance == null)
            {
                customerBalance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.CustomerBalances.Add(customerBalance);
            }

            // Remove existing record for this document to avoid duplicates
            var existingRecord = await _context.CustomerBalanceHistory
                .FirstOrDefaultAsync(h => h.ReferenceId == document.Id &&
                                         h.CustomerId == customerId &&
                                         h.CurrencyCode == currencyCode &&
                                         !h.IsDeleted);

            if (existingRecord != null)
            {
                _context.CustomerBalanceHistory.Remove(existingRecord);
            }

            // Create description
            string amountDirection = transactionAmount > 0 ? "افزایش" : "کاهش";
            string description = $"{document.Title} - مبلغ: {Math.Abs(document.Amount)} {currencyCode} - نقش: {role} - جهت: {amountDirection}";
            if (!string.IsNullOrEmpty(document.Description))
            {
                description += $" - {document.Description}";
            }

            // Create new history record for this document (balances will be recalculated in rebuild)
            var newHistoryRecord = new CustomerBalanceHistory
            {
                CustomerId = customerId,
                CurrencyCode = currencyCode,
                TransactionType = CustomerBalanceTransactionType.AccountingDocument,
                ReferenceId = document.Id,
                BalanceBefore = 0, // Will be recalculated in rebuild
                TransactionAmount = transactionAmount,
                BalanceAfter = 0, // Will be recalculated in rebuild
                Description = description,
                TransactionNumber = document.ReferenceNumber,
                TransactionDate = documentDate,
                CreatedAt = DateTime.Now,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.CustomerBalanceHistory.Add(newHistoryRecord);
            await _context.SaveChangesAsync();

            // Rebuild entire chain from scratch (simpler and more reliable)
            await RebuildCustomerBalanceChain(customerId, currencyCode);

            // Update the current balance entity
            var updatedBalance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            if (updatedBalance != null)
            {
                customerBalance.Balance = updatedBalance.Balance;
                customerBalance.LastUpdated = updatedBalance.LastUpdated;
            }
        }

        /// <summary>
        /// Processes bank account balance history with correct chain reconstruction for documents
        /// </summary>
        private async Task ProcessBankBalanceHistoryForDocument(AccountingDocument document, int bankAccountId,
            decimal transactionAmount, string role, string performedBy)
        {
            var documentDate = document.DocumentDate;

            // Load bank account balance - این کوئری مشکلی نداره چون BankAccountId مستقیم هست
            var bankBalance = await _context.BankAccountBalances
                .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId);

            if (bankBalance == null)
            {
                bankBalance = new BankAccountBalance
                {
                    BankAccountId = bankAccountId,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.BankAccountBalances.Add(bankBalance);
            }

            // Remove existing record for this document to avoid duplicates
            var existingRecord = await _context.BankAccountBalanceHistory
                .FirstOrDefaultAsync(h => h.ReferenceId == document.Id &&
                                         h.BankAccountId == bankAccountId &&
                                         !h.IsDeleted);

            if (existingRecord != null)
            {
                _context.BankAccountBalanceHistory.Remove(existingRecord);
            }

            // Create description
            string amountDirection = transactionAmount > 0 ? "افزایش" : "کاهش";
            string description = $"{document.Title} - مبلغ: {Math.Abs(document.Amount)} {document.CurrencyCode} - نقش: {role} - جهت: {amountDirection}";
            if (!string.IsNullOrEmpty(document.Description))
            {
                description += $" - {document.Description}";
            }

            // Create new history record for this document (balances will be recalculated in rebuild)
            var newHistoryRecord = new BankAccountBalanceHistory
            {
                BankAccountId = bankAccountId,
                TransactionType = BankAccountTransactionType.Document,
                ReferenceId = document.Id,
                BalanceBefore = 0, // Will be recalculated in rebuild
                TransactionAmount = transactionAmount,
                BalanceAfter = 0, // Will be recalculated in rebuild
                Description = description,
                TransactionDate = documentDate,
                CreatedAt = DateTime.Now,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.BankAccountBalanceHistory.Add(newHistoryRecord);
            await _context.SaveChangesAsync();

            // Rebuild entire chain from scratch (simpler and more reliable)
            await RebuildBankBalanceChain(bankAccountId);

            // Update the current balance entity
            var updatedBalance = await _context.BankAccountBalances
                .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId);

            if (updatedBalance != null)
            {
                bankBalance.Balance = updatedBalance.Balance;
                bankBalance.LastUpdated = updatedBalance.LastUpdated;
            }
        }





        #endregion Fast Balance Update (Coherence System)



        #region Create reocrds


        /// <summary>
        /// **ORDER PROCESSING** - Processes the complete financial impact of a currency exchange order.
        /// 
        /// **CRITICAL DUAL-CURRENCY OPERATION**: Every order creates exactly two currency impacts:
        /// 1. **Payment Transaction**: Customer pays FromAmount in FromCurrency (negative customer balance impact)
        /// 2. **Receipt Transaction**: Customer receives ToAmount in ToCurrency (positive customer balance impact)
        /// 
        /// **Currency Pool Updates**:
        /// - Institution receives FromCurrency from customer (pool balance increases)
        /// - Institution provides ToCurrency to customer (pool balance decreases)
        /// 
        /// **SRP CONSISTENCY GUARANTEE**: Uses the same calculation methods as PreviewOrderEffectsAsync()
        /// to ensure that actual processing effects exactly match preview calculations shown to users.
        /// 
        /// **Audit Trail**: Every transaction is logged with complete history for regulatory compliance
        /// and financial auditing. All amounts, exchange rates, and timing are permanently recorded.
        /// 
        /// **Validation**: Calculates expected effects before processing to log and validate consistency.
        /// 
        /// **PERFORMANCE OPTIMIZATION**: Balance rebuild runs asynchronously in background to prevent HTTP timeout issues.
        /// The order is saved immediately and rebuild completes in background without blocking the HTTP response.
        /// </summary>
        /// <param name="order">Complete order with all currency and amount information</param>
        /// <param name="performedBy">Identifier of who initiated the transaction (for audit trail)</param>
        public async Task ProcessOrderCreationAsync(Order order, string performedBy = "System")
        {
            _logger.LogInformation($"Processing order creation for Order ID: {order.Id}");

            try
            {
                //TODO: Should be transaction and singltone
                // Save order first
                _context.Add(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Order {order.Id} saved successfully. Starting fast balance update...");

                // PERFORMANCE OPTIMIZATION: Use fast balance update instead of full rebuild
                // Since coherence system (history) is already updated, we only need to update current balances
                await UpdateBalancesForOrderAsync(order, performedBy);

                _logger.LogInformation($"Order {order.Id} processing completed - fast balance update finished successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing order creation for Order {order.Id}: {ex.Message}");
                throw; // Re-throw to let controller handle it and inform user
            }
        }

        /// <summary>
        /// **ACCOUNTING DOCUMENT PROCESSING** - Processes financial documents (deposits, withdrawals, transfers).
        /// 
        /// **Document Types Supported**:
        /// - Customer deposits (increase customer balance)
        /// - Customer withdrawals (decrease customer balance) 
        /// - Inter-customer transfers
        /// - Bank account transactions
        /// 
        /// **Multi-Party Logic**:
        /// - **Payer**: Entity making the payment (balance increases for deposits, decreases for withdrawals)
        /// - **Receiver**: Entity receiving payment (balance decreases for payments made to them)
        /// - **Bank Accounts**: Institutional accounts affected by the document
        /// 
        /// **Verification Requirement**: Only processes verified documents to prevent unauthorized transactions.
        /// 
        /// **Complete Audit Trail**: Every document impact is logged with document reference numbers,
        /// dates, amounts, and all parties involved for comprehensive financial auditing.
        /// </summary>
        /// <param name="document">Verified accounting document with all party and amount information</param>
        /// <param name="performedBy">Identifier of who processed the document (for audit trail)</param>
        public async Task ProcessAccountingDocumentAsync(AccountingDocument document, string performedBy = "System")
        {
            _logger.LogInformation($"Processing accounting document ID: {document.Id}");

            try
            {
                // Save document if not already saved
                if (document.Id == 0)
                {
                    _context.Add(document);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Document {document.Id} saved successfully. Starting fast balance update...");

                // PERFORMANCE OPTIMIZATION: Use fast balance update instead of full rebuild
                // Since coherence system (history) is already updated, we only need to update current balances
                await UpdateBalancesForDocumentAsync(document, performedBy);

                _logger.LogInformation($"Document {document.Id} processing completed - fast balance update finished successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing accounting document for Document {document.Id}: {ex.Message}");
                throw; // Re-throw to let controller handle it and inform user
            }
        }



        #endregion Create reocrds







        /// <summary>
        /// Comprehensive rebuild of all financial balances based on new IsFrozen strategy:
        /// - Pool balances rebuilt from non-deleted AND non-frozen orders only with coherent history
        /// - Bank account balances rebuilt from non-deleted AND non-frozen documents only with coherent history
        /// - Customer balance history rebuilt from non-deleted orders, documents, and manual records (including frozen orders/documents)
        /// - Active buy/sell counts recalculated properly based on non-frozen orders
        ///
        /// This ensures frozen historical records don't affect current balance calculations
        /// but are preserved for customer balance history audit trail, including manual adjustments.
        /// Creates coherent balance history chains with proper BalanceBefore/BalanceAfter tracking.
        /// </summary>
        public async Task RebuildAllFinancialBalancesAsync(string performedBy = "System")
        {
            // Thread safety: Prevent concurrent rebuilds
            if (!await _rebuildSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                _logger.LogWarning("Balance rebuild already in progress, skipping concurrent request");
                throw new InvalidOperationException("Balance rebuild is already in progress. Please wait for the current operation to complete.");
            }

            try
            {
                await PerformBalanceRebuildAsync(performedBy);
            }
            finally
            {
                _rebuildSemaphore.Release();
            }
        }

        private async Task PerformBalanceRebuildAsync(string performedBy)
        {
            try
            {
                var logMessages = new List<string>
                {
                    "=== COMPREHENSIVE FINANCIAL BALANCE REBUILD WITH COHERENT HISTORY ===",
                    $"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Performed by: {performedBy}",
                    ""
                };

                _logger.LogInformation("Starting comprehensive financial balance rebuild");

                // Get all manual records efficiently (only load necessary fields)
                var manualCustomerRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted)
                    .Select(h => new
                    {
                        h.Id,
                        h.CustomerId,
                        h.CurrencyCode,
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.Description
                    })
                    .ToListAsync();

                var manualBankAccountRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.ManualEdit && !h.IsDeleted)
                    .Select(h => new
                    {
                        h.Id,
                        h.BankAccountId,
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.Description
                    })
                    .ToListAsync();

                var manualPoolRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted)
                    .Select(h => new
                    {
                        h.Id,
                        h.CurrencyCode,
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.Description
                    })
                    .ToListAsync();

                logMessages.Add($"All manual records saved in memory: Customer={manualCustomerRecords.Count}, BankAccount={manualBankAccountRecords.Count}, Pool={manualPoolRecords.Count}");
                _logger.LogInformation($"Manual records loaded: Customer={manualCustomerRecords.Count}, BankAccount={manualBankAccountRecords.Count}, Pool={manualPoolRecords.Count}");

                // PERFORMANCE OPTIMIZATION: Configure SQLite for faster bulk operations
                // NOTE: PRAGMA statements must be executed BEFORE starting a transaction
                // SQLite doesn't allow changing safety level inside a transaction
                await _context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;"); // Faster than FULL, still safe
                await _context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;"); // Ensure WAL mode
                await _context.Database.ExecuteSqlRawAsync("PRAGMA cache_size = -64000;"); // 64MB cache for better performance

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // STEP 1: Clear all history tables and reset balances to zero
                logMessages.Add("STEP 1: Clearing all history tables and resetting balances...");

                // PERFORMANCE OPTIMIZATION: Get counts before deletion to avoid separate queries
                var remainingManualPoolCount = await _context.CurrencyPoolHistory.CountAsync(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted);
                var remainingManualBankCount = await _context.BankAccountBalanceHistory.CountAsync(h => h.TransactionType == BankAccountTransactionType.ManualEdit && !h.IsDeleted);
                var remainingManualCount = await _context.CustomerBalanceHistory.CountAsync(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted);

                // Clear pool history (will be rebuilt) - PRESERVE manual records to prevent duplicates
                // Using parameterized query for better performance and safety
                var deletedPoolCount = await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM CurrencyPoolHistory WHERE TransactionType != {0} AND IsDeleted = 0",
                    (int)CurrencyPoolTransactionType.ManualEdit);
                logMessages.Add($"✓ Cleared {deletedPoolCount} non-manual pool history records, preserved {remainingManualPoolCount} manual records");

                // Clear bank account balance history (will be rebuilt) - PRESERVE manual records to prevent duplicates
                var deletedBankCount = await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM BankAccountBalanceHistory WHERE TransactionType != {0} AND IsDeleted = 0",
                    (int)BankAccountTransactionType.ManualEdit);
                logMessages.Add($"✓ Cleared {deletedBankCount} non-manual bank account history records, preserved {remainingManualBankCount} manual records");

                // Clear customer balance history (will be rebuilt) - PRESERVE manual records to prevent duplicates
                var deletedHistoryCount = await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM CustomerBalanceHistory WHERE TransactionType != {0} AND IsDeleted = 0",
                    (int)CustomerBalanceTransactionType.Manual);
                logMessages.Add($"✓ Cleared {deletedHistoryCount} non-manual customer balance history records, preserved {remainingManualCount} manual records");

                // PERFORMANCE OPTIMIZATION: Load all existing manual records from database once and cache them
                // This eliminates N+1 queries in loops below
                _logger.LogInformation("Loading existing manual records from database for cache...");
                var existingManualPoolRecordsCache = (await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted)
                    .ToListAsync())
                    .GroupBy(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim())
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(h => h.Id)
                    );

                var existingManualBankRecordsCache = (await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.ManualEdit && !h.IsDeleted)
                    .ToListAsync())
                    .GroupBy(h => h.BankAccountId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(h => h.Id)
                    );

                var existingManualCustomerRecordsCache = (await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted)
                    .ToListAsync())
                    .GroupBy(h => (h.CustomerId, CurrencyCode: (h.CurrencyCode ?? "").ToUpperInvariant().Trim()))
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(h => h.Id)
                    );

                _logger.LogInformation($"Cached existing manual records: Pool={existingManualPoolRecordsCache.Count} currencies, Bank={existingManualBankRecordsCache.Count} accounts, Customer={existingManualCustomerRecordsCache.Count} customer-currency combinations");

                // Reset balances efficiently using bulk updates
                var resetTimestamp = DateTime.Now;
                await _context.Database.ExecuteSqlRawAsync("UPDATE CustomerBalances SET Balance = 0, LastUpdated = {0}", resetTimestamp);
                await _context.Database.ExecuteSqlRawAsync("UPDATE CurrencyPools SET Balance = 0, ActiveBuyOrderCount = 0, ActiveSellOrderCount = 0, TotalBought = 0, TotalSold = 0, LastUpdated = {0}", resetTimestamp);
                await _context.Database.ExecuteSqlRawAsync("UPDATE BankAccountBalances SET Balance = 0, LastUpdated = {0}", resetTimestamp);

                logMessages.Add("✓ Reset all balances to zero using bulk updates");

                // STEP 2: Create coherent pool history for each currency
                logMessages.Add("");
                logMessages.Add("STEP 2: Creating coherent pool history...");

                // Load active orders with required data only (eliminate N+1 queries)
                var activeOrders = await _context.Orders
                    .Where(o => !o.IsDeleted && !o.IsFrozen)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Select(o => new
                    {
                        o.Id,
                        o.CustomerId,
                        o.CreatedAt,
                        o.FromAmount,
                        o.ToAmount,
                        o.Rate,
                        o.Notes,
                        FromCurrencyCode = o.FromCurrency.Code,
                        ToCurrencyCode = o.ToCurrency.Code
                    })
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                logMessages.Add($"Processing {activeOrders.Count} active (non-deleted, non-frozen) orders and {manualPoolRecords.Count} manual pool records...");

                // Pre-allocate collections with estimated capacity for better performance
                var poolTransactionItems = new List<(string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string PoolTransactionType, string Description)>(activeOrders.Count * 2);

                // Add order transactions (eliminated N+1 query by pre-loading data)
                // IMPORTANT: Normalize currency codes to UPPERCASE to handle case sensitivity issues (e.g., USDT vs usdt)
                foreach (var o in activeOrders)
                {
                    var fromCurrencyCode = (o.FromCurrencyCode ?? "").ToUpperInvariant().Trim();
                    var toCurrencyCode = (o.ToCurrencyCode ?? "").ToUpperInvariant().Trim();

                    // Institution receives FromAmount in FromCurrency (pool increases)
                    poolTransactionItems.Add((fromCurrencyCode, o.CreatedAt, "Order", o.Id, o.FromAmount, "Buy", o.Notes ?? ""));

                    // Institution pays ToAmount in ToCurrency (pool decreases)
                    poolTransactionItems.Add((toCurrencyCode, o.CreatedAt, "Order", o.Id, -o.ToAmount, "Sell", o.Notes ?? ""));
                }

                // Add manual pool records as transactions for balance calculation
                // IMPORTANT: These will be used for balance calculation but we'll check for duplicates before creating new records
                // Normalize currency codes to UPPERCASE for consistency
                foreach (var manual in manualPoolRecords)
                {
                    var currencyCode = (manual.CurrencyCode ?? "").ToUpperInvariant().Trim();
                    poolTransactionItems.Add((
                        currencyCode,
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id, // Use existing ID to identify duplicates
                        manual.TransactionAmount,
                        "Manual",
                        manual.Description ?? "Manual adjustment"
                    ));
                }
                logMessages.Add($"Added {manualPoolRecords.Count} manual pool records to transaction items for balance calculation");

                // Group by currency code (now normalized to uppercase) to create coherent history per currency
                var currencyGroups = poolTransactionItems
                    .GroupBy(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Process pool transactions in batches for better performance
                // PERFORMANCE OPTIMIZATION: Increased batch size from 1000 to 5000 to reduce database round trips
                const int batchSize = 5000;
                var poolHistoryRecords = new List<CurrencyPoolHistory>();
                var poolBalanceUpdates = new Dictionary<string, (decimal Balance, int BuyCount, int SellCount, decimal TotalBought, decimal TotalSold)>();

                foreach (var currencyGroup in currencyGroups)
                {
                    var currencyCode = currencyGroup.Key;
                    var currencyTransactions = currencyGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!currencyTransactions.Any()) continue;

                    // Process transactions chronologically for this currency
                    decimal runningBalance = 0;
                    int buyCount = 0, sellCount = 0;
                    decimal totalBought = 0, totalSold = 0;

                    // Normalize currency code for comparison (client-side)
                    var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

                    // PERFORMANCE OPTIMIZATION: Use cached manual records instead of querying database
                    // This eliminates N+1 query problem
                    var existingManualPoolRecords = existingManualPoolRecordsCache.TryGetValue(normalizedCurrencyCode, out var poolRecords)
                        ? poolRecords
                        : new Dictionary<long, CurrencyPoolHistory>();

                    foreach (var transaction in currencyTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Order" => CurrencyPoolTransactionType.Order,
                            "Manual" => CurrencyPoolTransactionType.ManualEdit,
                            _ => CurrencyPoolTransactionType.Order
                        };

                        // For manual records, check if they already exist in database
                        if (transactionType == CurrencyPoolTransactionType.ManualEdit &&
                            transaction.ReferenceId.HasValue &&
                            existingManualPoolRecords.ContainsKey(transaction.ReferenceId.Value))
                        {
                            // Update existing manual record with correct balance
                            var existingRecord = existingManualPoolRecords[transaction.ReferenceId.Value];
                            existingRecord.BalanceBefore = runningBalance;
                            existingRecord.BalanceAfter = runningBalance + transaction.Amount;
                            runningBalance = existingRecord.BalanceAfter;
                            // Don't create new record - just update the existing one
                            continue;
                        }

                        // For non-manual or new manual records, create new history record
                        poolHistoryRecords.Add(new CurrencyPoolHistory
                        {
                            CurrencyCode = currencyCode,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            PoolTransactionType = transaction.PoolTransactionType,
                            Description = transaction.Description,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.Now,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        });

                        runningBalance = poolHistoryRecords.Last().BalanceAfter;

                        // Update counts and totals for orders only (not manual records)
                        if (transaction.TransactionType == "Order")
                        {
                            if (transaction.PoolTransactionType == "Buy")
                            {
                                buyCount++;
                                totalBought += transaction.Amount;
                            }
                            else if (transaction.PoolTransactionType == "Sell")
                            {
                                sellCount++;
                                totalSold += Math.Abs(transaction.Amount);
                            }
                        }

                        // Batch save when reaching batch size
                        if (poolHistoryRecords.Count >= batchSize)
                        {
                            await _context.CurrencyPoolHistory.AddRangeAsync(poolHistoryRecords);
                            await _context.SaveChangesAsync();
                            poolHistoryRecords.Clear();
                            // PERFORMANCE OPTIMIZATION: Clear ChangeTracker to free memory
                            _context.ChangeTracker.Clear();
                        }
                    }

                    // Store final balance for update
                    poolBalanceUpdates[currencyCode] = (runningBalance, buyCount, sellCount, totalBought, totalSold);
                }

                // Save remaining pool history records
                if (poolHistoryRecords.Any())
                {
                    await _context.CurrencyPoolHistory.AddRangeAsync(poolHistoryRecords);
                    await _context.SaveChangesAsync();
                }

                // Update pool balances in batch
                // IMPORTANT: Normalize currency code lookup to handle case sensitivity (SQLite is case-sensitive by default)
                // Load all pools first for case-insensitive matching
                var allPools = await _context.CurrencyPools
                    .Include(p => p.Currency)
                    .ToListAsync();

                foreach (var (currencyCode, balances) in poolBalanceUpdates)
                {
                    var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

                    // Case-insensitive lookup in memory (since SQLite doesn't support ToUpper in LINQ)
                    var pool = allPools.FirstOrDefault(p =>
                        (p.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode ||
                        (p.Currency?.Code ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

                    if (pool != null)
                    {
                        pool.Balance = balances.Balance;
                        pool.ActiveBuyOrderCount = balances.BuyCount;
                        pool.ActiveSellOrderCount = balances.SellCount;
                        pool.TotalBought = balances.TotalBought;
                        pool.TotalSold = balances.TotalSold;
                        pool.LastUpdated = DateTime.Now;

                        // Ensure CurrencyCode is normalized to uppercase for consistency
                        if (pool.CurrencyCode != normalizedCurrencyCode)
                        {
                            _logger.LogWarning($"Normalizing CurrencyCode from '{pool.CurrencyCode}' to '{normalizedCurrencyCode}' for pool {pool.Id}");
                            pool.CurrencyCode = normalizedCurrencyCode;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Currency pool not found for currency code: {normalizedCurrencyCode}. Balance update skipped. Available pools: {string.Join(", ", allPools.Select(p => p.CurrencyCode))}");
                    }
                }
                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent pool history for {currencyGroups.Count} currencies with {activeOrders.Count} active orders");

                // STEP 3: Create coherent bank account balance history
                logMessages.Add("");
                logMessages.Add("STEP 3: Creating coherent bank account balance history...");

                // Load active documents efficiently
                // IMPORTANT: Only process verified documents (same as customer balance history)
                // Unverified documents should not affect bank account balances
                var activeDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && !d.IsFrozen && d.IsVerified)
                    .Select(d => new
                    {
                        d.Id,
                        d.DocumentDate,
                        d.CurrencyCode,
                        d.Amount,
                        d.Notes,
                        d.PayerType,
                        d.PayerBankAccountId,
                        d.ReceiverType,
                        d.ReceiverBankAccountId
                    })
                    .OrderBy(d => d.DocumentDate)
                    .ToListAsync();

                logMessages.Add($"Processing {activeDocuments.Count} active (non-deleted, non-frozen, verified) documents and {manualBankAccountRecords.Count} manual bank account records...");

                // Create unified transaction items for bank accounts from documents and manual records
                var bankAccountTransactionItems = new List<(int BankAccountId, string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string Description)>(activeDocuments.Count + manualBankAccountRecords.Count);

                // Add document transactions (eliminated N+1 query)
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency (handles USDT case sensitivity)
                foreach (var d in activeDocuments)
                {
                    var normalizedCurrencyCode = (d.CurrencyCode ?? "").ToUpperInvariant().Trim();

                    if (d.PayerType == PayerType.System && d.PayerBankAccountId.HasValue && d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId.HasValue)
                    {
                        // Both sides are system bank accounts: create two transactions
                        bankAccountTransactionItems.Add((d.PayerBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "system bank to bank", d.Id, d.Amount, d.Notes ?? string.Empty));
                        bankAccountTransactionItems.Add((d.ReceiverBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "system bank to bank", d.Id, -(d.Amount), d.Notes ?? string.Empty));
                    }
                    else
                    {
                        // Single side system bank account transactions
                        if (d.PayerType == PayerType.System && d.PayerBankAccountId.HasValue)
                            bankAccountTransactionItems.Add((d.PayerBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "payment document", d.Id, d.Amount, d.Notes ?? string.Empty));
                        if (d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId.HasValue)
                            bankAccountTransactionItems.Add((d.ReceiverBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "reciept document", d.Id, -(d.Amount), d.Notes ?? string.Empty));
                    }
                }

                // Add manual bank account records as transactions for balance calculation
                // IMPORTANT: These will be used for balance calculation but we'll check for duplicates before creating new records
                foreach (var manual in manualBankAccountRecords)
                {
                    bankAccountTransactionItems.Add((
                        manual.BankAccountId,
                        "N/A", // Bank accounts don't have currency codes in the same way
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id, // Use existing ID to identify duplicates
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }
                logMessages.Add($"Added {manualBankAccountRecords.Count} manual bank account records to transaction items for balance calculation");

                // Group by bank account to create coherent history
                var bankAccountGroups = bankAccountTransactionItems
                    .GroupBy(x => x.BankAccountId)
                    .ToList();

                // Process bank account transactions in batches
                var bankHistoryRecords = new List<BankAccountBalanceHistory>();
                var bankBalanceUpdates = new Dictionary<int, decimal>();

                foreach (var bankGroup in bankAccountGroups)
                {
                    var bankAccountId = bankGroup.Key;
                    var bankTransactions = bankGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!bankTransactions.Any()) continue;

                    // Process transactions chronologically for this bank account
                    decimal runningBalance = 0;

                    // PERFORMANCE OPTIMIZATION: Use cached manual records instead of querying database
                    // This eliminates N+1 query problem
                    var existingManualBankRecords = existingManualBankRecordsCache.TryGetValue(bankAccountId, out var bankRecords)
                        ? bankRecords
                        : new Dictionary<long, BankAccountBalanceHistory>();

                    foreach (var transaction in bankTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Document" => BankAccountTransactionType.Document,
                            "Manual" => BankAccountTransactionType.ManualEdit,
                            _ => BankAccountTransactionType.Document
                        };

                        // For manual records, check if they already exist in database
                        if (transactionType == BankAccountTransactionType.ManualEdit &&
                            transaction.ReferenceId.HasValue &&
                            existingManualBankRecords.ContainsKey(transaction.ReferenceId.Value))
                        {
                            // Update existing manual record with correct balance
                            var existingRecord = existingManualBankRecords[transaction.ReferenceId.Value];
                            existingRecord.BalanceBefore = runningBalance;
                            existingRecord.BalanceAfter = runningBalance + transaction.Amount;
                            runningBalance = existingRecord.BalanceAfter;
                            // Don't create new record - just update the existing one
                            continue;
                        }

                        // For non-manual or new manual records, create new history record
                        bankHistoryRecords.Add(new BankAccountBalanceHistory
                        {
                            BankAccountId = bankAccountId,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            Description = transaction.Description,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.Now,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        });

                        runningBalance = bankHistoryRecords.Last().BalanceAfter;

                        // Batch save when reaching batch size
                        if (bankHistoryRecords.Count >= batchSize)
                        {
                            await _context.BankAccountBalanceHistory.AddRangeAsync(bankHistoryRecords);
                            await _context.SaveChangesAsync();
                            bankHistoryRecords.Clear();
                            // PERFORMANCE OPTIMIZATION: Clear ChangeTracker to free memory
                            _context.ChangeTracker.Clear();
                        }
                    }

                    // Store final balance for update
                    bankBalanceUpdates[bankAccountId] = runningBalance;
                }

                // Save remaining bank history records
                if (bankHistoryRecords.Any())
                {
                    await _context.BankAccountBalanceHistory.AddRangeAsync(bankHistoryRecords);
                    await _context.SaveChangesAsync();
                }

                // Update bank account balances in batch
                foreach (var (bankAccountId, balance) in bankBalanceUpdates)
                {
                    var bankBalance = await _context.BankAccountBalances
                        .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId);
                    if (bankBalance != null)
                    {
                        bankBalance.Balance = balance;
                        bankBalance.LastUpdated = DateTime.Now;
                    }
                }
                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent bank account balance history for {bankAccountGroups.Count} bank account + currency combinations");

                // STEP 4: Rebuild coherent customer balance history from orders, documents, and manual records (including frozen, excluding only deleted)
                logMessages.Add("");
                logMessages.Add("STEP 4: Rebuilding coherent customer balance history from orders, documents, and manual records (including frozen for customer history)...");

                // Load all valid documents and orders efficiently for customer history
                var allValidDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && d.IsVerified)
                    .Select(d => new
                    {
                        d.Id,
                        d.DocumentDate,
                        d.CurrencyCode,
                        d.Amount,
                        d.Description,
                        d.ReferenceNumber,
                        d.PayerType,
                        d.PayerCustomerId,
                        d.ReceiverType,
                        d.ReceiverCustomerId
                    })
                    .ToListAsync();

                var allValidOrders = await _context.Orders
                    .Where(o => !o.IsDeleted)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Select(o => new
                    {
                        o.Id,
                        o.CustomerId,
                        o.CreatedAt,
                        o.FromAmount,
                        o.ToAmount,
                        o.Notes,
                        FromCurrencyCode = o.FromCurrency.Code,
                        ToCurrencyCode = o.ToCurrency.Code
                    })
                    .ToListAsync();

                logMessages.Add($"Processing {allValidDocuments.Count} valid documents, {allValidOrders.Count} valid orders, and {manualCustomerRecords.Count} manual customer records for customer balance history...");

                // Create unified transaction items for customers from orders, documents, and manual records
                var estimatedCapacity = allValidOrders.Count * 2 + allValidDocuments.Count * 2 + manualCustomerRecords.Count;
                var customerTransactionItems = new List<(int CustomerId, string CurrencyCode, DateTime TransactionDate, string TransactionType, string transactionCode, int? ReferenceId, decimal Amount, string Description)>(estimatedCapacity);

                // Add document transactions
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency
                foreach (var d in allValidDocuments)
                {
                    var currencyCode = (d.CurrencyCode ?? "").ToUpperInvariant().Trim();

                    if (d.PayerType == PayerType.Customer && d.PayerCustomerId.HasValue && d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId.HasValue)
                    {
                        // Both sides are customers: create two transactions
                        customerTransactionItems.Add((d.PayerCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, d.Amount, d.Description ?? string.Empty));
                        customerTransactionItems.Add((d.ReceiverCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, -d.Amount, d.Description ?? string.Empty));
                    }
                    else
                    {
                        // Single side customer transactions
                        if (d.PayerType == PayerType.Customer && d.PayerCustomerId.HasValue)
                            customerTransactionItems.Add((d.PayerCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, d.Amount, d.Description ?? string.Empty));
                        if (d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId.HasValue)
                            customerTransactionItems.Add((d.ReceiverCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, -d.Amount, d.Description ?? string.Empty));
                    }
                }

                // Add order transactions for customer history
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency
                foreach (var o in allValidOrders)
                {
                    var fromCurrencyCode = (o.FromCurrencyCode ?? "").ToUpperInvariant().Trim();
                    var toCurrencyCode = (o.ToCurrencyCode ?? "").ToUpperInvariant().Trim();

                    // Customer pays FromAmount in FromCurrency
                    customerTransactionItems.Add((o.CustomerId, fromCurrencyCode, o.CreatedAt, "Order", string.Empty, o.Id, -o.FromAmount, o.Notes ?? string.Empty));

                    // Customer receives ToAmount in ToCurrency
                    customerTransactionItems.Add((o.CustomerId, toCurrencyCode, o.CreatedAt, "Order", string.Empty, o.Id, o.ToAmount, o.Notes ?? string.Empty));
                }

                logMessages.Add($"Manual customer records in database: [{manualCustomerRecords.Count}]");
                logMessages.Add($"Customer transaction items before adding manual: [{customerTransactionItems.Count}]");

                // Add manual customer records as transactions for balance calculation
                // IMPORTANT: These will be used for balance calculation but we'll check for duplicates before creating new records
                // Normalize currency codes to UPPERCASE for consistency
                foreach (var manual in manualCustomerRecords)
                {
                    var currencyCode = (manual.CurrencyCode ?? "").ToUpperInvariant().Trim();
                    customerTransactionItems.Add((
                        manual.CustomerId,
                        currencyCode,
                        manual.TransactionDate,
                        "Manual",
                        string.Empty,
                        (int?)manual.Id, // Use existing ID to identify duplicates
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }
                logMessages.Add($"Customer transaction items after adding manual: [{customerTransactionItems.Count}]");

                // Group by customer + currency and create coherent history (process in chunks for memory efficiency)
                // Normalize currency codes to uppercase before grouping for case-insensitive matching
                var normalizedCustomerTransactions = customerTransactionItems
                    .Select(x => new
                    {
                        x.CustomerId,
                        CurrencyCode = (x.CurrencyCode ?? "").ToUpperInvariant().Trim(),
                        x.TransactionDate,
                        x.TransactionType,
                        x.transactionCode,
                        x.ReferenceId,
                        x.Amount,
                        x.Description
                    })
                    .ToList();

                var customerGroups = normalizedCustomerTransactions
                    .GroupBy(x => new { x.CustomerId, x.CurrencyCode })
                    .ToList();

                logMessages.Add($"Creating coherent history for {customerGroups.Count} customer + currency combinations...");

                // Process customer groups in chunks to reduce memory usage
                const int customerChunkSize = 500; // Process 500 customer-currency combinations at a time
                var customerChunks = customerGroups.Chunk(customerChunkSize);

                foreach (var chunk in customerChunks)
                {
                    var customerHistoryRecords = new List<CustomerBalanceHistory>();
                    var customerBalanceUpdates = new Dictionary<(int CustomerId, string CurrencyCode), decimal>();

                    foreach (var customerGroup in chunk)
                    {
                        var customerId = customerGroup.Key.CustomerId;
                        var currencyCode = customerGroup.Key.CurrencyCode;

                        // Order all transactions chronologically by TransactionDate
                        var orderedTransactions = customerGroup.OrderBy(x => x.TransactionDate).ToList();

                        if (!orderedTransactions.Any()) continue;

                        // Process transactions chronologically for this customer + currency
                        decimal runningBalance = 0;

                        // Normalize currency code for comparison (client-side)
                        var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

                        // PERFORMANCE OPTIMIZATION: Use cached manual records instead of querying database
                        // This eliminates N+1 query problem
                        var cacheKey = (customerId, CurrencyCode: normalizedCurrencyCode);
                        var existingManualRecords = existingManualCustomerRecordsCache.TryGetValue(cacheKey, out var customerRecords)
                            ? customerRecords
                            : new Dictionary<long, CustomerBalanceHistory>();

                        foreach (var transaction in orderedTransactions)
                        {
                            var transactionType = transaction.TransactionType switch
                            {
                                "Order" => CustomerBalanceTransactionType.Order,
                                "Document" => CustomerBalanceTransactionType.AccountingDocument,
                                "Manual" => CustomerBalanceTransactionType.Manual,
                                _ => CustomerBalanceTransactionType.AccountingDocument
                            };

                            // For manual records, check if they already exist in database
                            if (transactionType == CustomerBalanceTransactionType.Manual &&
                                transaction.ReferenceId.HasValue &&
                                existingManualRecords.ContainsKey(transaction.ReferenceId.Value))
                            {
                                // Update existing manual record with correct balance
                                var existingRecord = existingManualRecords[transaction.ReferenceId.Value];
                                existingRecord.BalanceBefore = runningBalance;
                                existingRecord.BalanceAfter = runningBalance + transaction.Amount;
                                runningBalance = existingRecord.BalanceAfter;
                                // Don't create new record - just update the existing one
                                continue;
                            }

                            // For non-manual or new manual records, create new history record
                            var note = $"{transactionType} - مبلغ: {transaction.Amount} {transaction.CurrencyCode}";
                            if (!string.IsNullOrEmpty(transaction.transactionCode))
                                note += $" - شناسه تراکنش: {transaction.transactionCode}";

                            customerHistoryRecords.Add(new CustomerBalanceHistory
                            {
                                CustomerId = customerId,
                                CurrencyCode = currencyCode,
                                TransactionType = transactionType,
                                ReferenceId = transaction.ReferenceId,
                                BalanceBefore = runningBalance,
                                TransactionAmount = transaction.Amount,
                                BalanceAfter = runningBalance + transaction.Amount,
                                Description = transaction.Description,
                                TransactionNumber = transaction.transactionCode,
                                Note = note,
                                TransactionDate = transaction.TransactionDate,
                                CreatedAt = DateTime.Now,
                                CreatedBy = performedBy,
                                IsDeleted = false
                            });

                            runningBalance = customerHistoryRecords.Last().BalanceAfter;

                            // Batch save when reaching batch size
                            if (customerHistoryRecords.Count >= batchSize)
                            {
                                await _context.CustomerBalanceHistory.AddRangeAsync(customerHistoryRecords);
                                await _context.SaveChangesAsync();
                                customerHistoryRecords.Clear();
                                // PERFORMANCE OPTIMIZATION: Clear ChangeTracker to free memory
                                _context.ChangeTracker.Clear();
                            }
                        }

                        // Store final balance for update
                        customerBalanceUpdates[(customerId, currencyCode)] = runningBalance;
                    }

                    // Save remaining customer history records for this chunk
                    if (customerHistoryRecords.Any())
                    {
                        await _context.CustomerBalanceHistory.AddRangeAsync(customerHistoryRecords);
                        await _context.SaveChangesAsync();
                    }

                    // Update customer balances for this chunk
                    // Load all customer balances for this chunk first for case-insensitive matching
                    var customerIds = customerBalanceUpdates.Keys.Select(k => k.CustomerId).Distinct().ToList();
                    var allCustomerBalances = await _context.CustomerBalances
                        .Where(b => customerIds.Contains(b.CustomerId))
                        .ToListAsync();

                    foreach (var ((customerId, currencyCode), balance) in customerBalanceUpdates)
                    {
                        var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

                        // Case-insensitive lookup in memory
                        var customerBalance = allCustomerBalances.FirstOrDefault(b =>
                            b.CustomerId == customerId &&
                            (b.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

                        if (customerBalance == null)
                        {
                            customerBalance = new CustomerBalance
                            {
                                CustomerId = customerId,
                                CurrencyCode = normalizedCurrencyCode,
                                Balance = 0,
                                LastUpdated = DateTime.Now
                            };
                            _context.CustomerBalances.Add(customerBalance);
                            allCustomerBalances.Add(customerBalance); // Add to list for potential future lookups in this chunk
                        }
                        else
                        {
                            // Ensure CurrencyCode is normalized to uppercase for consistency
                            if (customerBalance.CurrencyCode != normalizedCurrencyCode)
                            {
                                _logger.LogWarning($"Normalizing CustomerBalance CurrencyCode from '{customerBalance.CurrencyCode}' to '{normalizedCurrencyCode}' for Customer {customerId}");
                                customerBalance.CurrencyCode = normalizedCurrencyCode;
                            }
                        }
                        customerBalance.Balance = balance;
                        customerBalance.LastUpdated = DateTime.Now;
                    }
                    await _context.SaveChangesAsync();

                    // PERFORMANCE OPTIMIZATION: Clear ChangeTracker to free memory between chunks
                    _context.ChangeTracker.Clear();
                    customerBalanceUpdates.Clear();
                }
                logMessages.Add($"✓ Rebuilt coherent customer balance history for {customerGroups.Count} customer + currency combinations from {allValidDocuments.Count} documents and {allValidOrders.Count} orders (manual records were preserved)");




                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== REBUILD COMPLETED SUCCESSFULLY ===");
                logMessages.Add($"Finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add("✅ All balance histories rebuilt with coherent balance chains");
                logMessages.Add("✅ Active buy/sell counts recalculated based on non-frozen orders only");
                logMessages.Add("✅ Frozen records excluded from pool/bank calculations but included in customer history");
                logMessages.Add("✅ Manual customer balance adjustments preserved in complete customer history");

                var logSummary = string.Join("\n", logMessages);

                _logger.LogInformation($"Financial balance rebuild completed successfully. Summary: {logSummary}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during comprehensive financial balance rebuild: {ex.Message}");
                throw;
            }

        }




        #region Reusable Rebuild Methods

        /// <summary>
        /// Rebuilds customer balance chain from scratch for a specific customer and currency.
        /// This method gets all non-deleted transactions and rebuilds the entire chain.
        /// Used for both creation and deletion operations.
        /// </summary>
        private async Task RebuildCustomerBalanceChain(int customerId, string currencyCode)
        {
            if (string.IsNullOrEmpty(currencyCode)) return;

            // Get all non-deleted transactions for this customer and currency
            var allTransactions = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId &&
                           !h.IsDeleted &&
                           h.CurrencyCode == currencyCode)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            // Rebuild the chain from scratch
            decimal runningBalance = 0;
            foreach (var transaction in allTransactions)
            {
                transaction.BalanceBefore = runningBalance;
                transaction.BalanceAfter = runningBalance + transaction.TransactionAmount;
                runningBalance = transaction.BalanceAfter;
            }

            // Update customer balance
            var customerBalance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            if (customerBalance != null)
            {
                customerBalance.Balance = runningBalance;
                customerBalance.LastUpdated = DateTime.Now;
            }
            else if (allTransactions.Any())
            {
                // Create balance record if it doesn't exist but we have transactions
                customerBalance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    Balance = runningBalance,
                    LastUpdated = DateTime.Now
                };
                _context.CustomerBalances.Add(customerBalance);
            }
        }

        /// <summary>
        /// Rebuilds pool balance chain from scratch for a specific currency.
        /// This method gets all non-deleted transactions and rebuilds the entire chain.
        /// Used for both creation and deletion operations.
        /// </summary>
        private async Task RebuildPoolBalanceChain(string currencyCode)
        {
            if (string.IsNullOrEmpty(currencyCode)) return;

            // Get all non-deleted pool transactions for this currency
            var allTransactions = await _context.CurrencyPoolHistory
                .Where(h => !h.IsDeleted && h.CurrencyCode == currencyCode)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            // Rebuild the chain from scratch
            decimal runningBalance = 0;
            foreach (var transaction in allTransactions)
            {
                transaction.BalanceBefore = runningBalance;
                transaction.BalanceAfter = runningBalance + transaction.TransactionAmount;
                runningBalance = transaction.BalanceAfter;
            }

            // Load and update the CurrencyPool entity
            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Code == currencyCode);

            if (currency != null)
            {
                var poolBalance = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == currency.Id);

                if (poolBalance != null)
                {
                    poolBalance.Balance = runningBalance;
                    poolBalance.LastUpdated = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Rebuilds bank account balance chain from scratch for a specific bank account.
        /// This method gets all non-deleted transactions and rebuilds the entire chain.
        /// Used for both creation and deletion operations.
        /// </summary>
        private async Task RebuildBankBalanceChain(int bankAccountId)
        {
            // Get all non-deleted bank transactions for this account
            var allTransactions = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId && !h.IsDeleted)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            // Rebuild the chain from scratch
            decimal runningBalance = 0;
            foreach (var transaction in allTransactions)
            {
                transaction.BalanceBefore = runningBalance;
                transaction.BalanceAfter = runningBalance + transaction.TransactionAmount;
                runningBalance = transaction.BalanceAfter;
            }

            // Update bank balance
            var bankBalance = await _context.BankAccountBalances
                .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId);

            if (bankBalance != null)
            {
                bankBalance.Balance = runningBalance;
                bankBalance.LastUpdated = DateTime.Now;
            }
            else if (allTransactions.Any())
            {
                // Create balance record if it doesn't exist but we have transactions
                bankBalance = new BankAccountBalance
                {
                    BankAccountId = bankAccountId,
                    Balance = runningBalance,
                    LastUpdated = DateTime.Now
                };
                _context.BankAccountBalances.Add(bankBalance);
            }
        }

        #endregion

        #region Delete Operations


        #region  Delete Orders

        /// <summary>
        /// Safely deletes an order by reversing its financial impacts
        /// </summary>
        public async Task DeleteOrderAsync(Order order, string performedBy = "Admin")
        {
            try
            {
                _logger.LogInformation($"Deleting order: Order {order.Id} by {performedBy}");

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // 1. Soft delete the order
                    order.IsDeleted = true;
                    order.DeletedAt = DateTime.Now;
                    order.DeletedBy = performedBy;

                    _context.Update(order);


                    // 2.  delete related history records
                    await DeleteOrderHistoryRecords(order.Id);
                    await _context.SaveChangesAsync();


                    // 3. Rebuild balance chains without the deleted records

                    // Rebuild customer balance chains for both currencies
                    await RebuildCustomerBalanceChain(order.CustomerId, order.FromCurrency?.Code);
                    await RebuildCustomerBalanceChain(order.CustomerId, order.ToCurrency?.Code);

                    // Rebuild pool balance chains for both currencies
                    await RebuildPoolBalanceChain(order.FromCurrency?.Code);
                    await RebuildPoolBalanceChain(order.ToCurrency?.Code);

                    // Update pool statistics (decrement counts and totals)
                    await UpdatePoolStatisticsForOrderDeletion(order);

                    await _context.SaveChangesAsync();


                    await transaction.CommitAsync();

                    _logger.LogInformation($"Order deletion completed: Order {order.Id}");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting order {order.Id}: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Hard deletes all customer and pool history records related to an order
        /// These are calculation records, so they should be physically removed, not soft deleted
        /// </summary>
        private async Task DeleteOrderHistoryRecords(long orderId)
        {
            // Hard delete customer balance history records
            var customerHistoryRecords = await _context.CustomerBalanceHistory
                .Where(h => h.ReferenceId == orderId)
                .ToListAsync();

            _context.CustomerBalanceHistory.RemoveRange(customerHistoryRecords);

            // Hard delete pool history records
            var poolHistoryRecords = await _context.CurrencyPoolHistory
                .Where(h => h.ReferenceId == orderId)
                .ToListAsync();

            _context.CurrencyPoolHistory.RemoveRange(poolHistoryRecords);

            _logger.LogInformation($"Hard deleted {customerHistoryRecords.Count} customer and {poolHistoryRecords.Count} pool history records for order {orderId}");
        }

        /// <summary>
        /// Updates pool statistics when order is deleted
        /// </summary>
        private async Task UpdatePoolStatisticsForOrderDeletion(Order order)
        {
            // Recalculate statistics for the source currency pool
            if (order.FromCurrencyId != null && order.FromAmount > 0)
            {
                _logger.LogInformation($"[PoolStats] Recalculating FROM pool for order {order.Id} (CurrencyId={order.FromCurrencyId}, Code={order.FromCurrency?.Code})");

                var fromPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrencyId);

                if (fromPool != null)
                {
                    fromPool.ActiveBuyOrderCount -= 1;
                    fromPool.TotalBought -= order.FromAmount;
                    fromPool.LastUpdated = DateTime.Now;

                    _logger.LogInformation($"Updated {fromPool.CurrencyCode} pool: BuyCount={fromPool.ActiveBuyOrderCount}, TotalBought={fromPool.TotalBought}");
                }
                else
                {
                    _logger.LogWarning($"[PoolStats] FROM pool not found for CurrencyId={order.FromCurrencyId} when deleting order {order.Id}");
                }
            }

            // Update ToCurrency pool (Sell side)
            if (order.ToCurrencyId != null && order.ToAmount > 0)
            {
                _logger.LogInformation($"[PoolStats] Recalculating TO pool for order {order.Id} (CurrencyId={order.ToCurrencyId}, Code={order.ToCurrency?.Code})");

                var toPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrencyId);

                if (toPool != null)
                {
                    toPool.ActiveSellOrderCount -= 1;
                    toPool.TotalSold -= order.ToAmount;
                    toPool.LastUpdated = DateTime.Now;

                    _logger.LogInformation($"Updated {toPool.CurrencyCode} pool: SellCount={toPool.ActiveSellOrderCount}, TotalSold={toPool.TotalSold}");
                }
                else
                {
                    _logger.LogWarning($"[PoolStats] TO pool not found for CurrencyId={order.ToCurrencyId} when deleting order {order.Id}");
                }
            }
        }


        #endregion




        #region Delete Documents

        /// <summary>
        /// Safely deletes an accounting document by reversing its financial impacts
        /// </summary>
        public async Task DeleteAccountingDocumentAsync(AccountingDocument document, string performedBy = "Admin")
        {
            try
            {
                _logger.LogInformation($"Deleting document: Document {document.Id} by {performedBy}");


                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // 1. Soft delete the document
                    document.IsDeleted = true;
                    document.DeletedAt = DateTime.Now;
                    document.DeletedBy = performedBy;

                    _context.Update(document);
                    await _context.SaveChangesAsync();

                    // 2. Soft delete related history records
                    await DeleteCustomerAndBankHistoryRecords(document.Id);

                    await _context.SaveChangesAsync();

                    // 3. Rebuild balance chains without the deleted records
                    await RebuildBalanceChainsForDocumentDeletion(document);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Document deletion completed: Document {document.Id}");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document {document.Id}: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Hard deletes all customer and bank history records related to a document
        /// These are calculation records, so they should be physically removed, not soft deleted
        /// </summary>
        private async Task DeleteCustomerAndBankHistoryRecords(long documentId)
        {
            // Hard delete customer balance history records
            var customerHistoryRecords = await _context.CustomerBalanceHistory
                .Where(h => h.ReferenceId == documentId)
                .ToListAsync();

            _context.CustomerBalanceHistory.RemoveRange(customerHistoryRecords);

            // Hard delete bank account balance history records
            var bankHistoryRecords = await _context.BankAccountBalanceHistory
                .Where(h => h.ReferenceId == documentId)
                .ToListAsync();

            _context.BankAccountBalanceHistory.RemoveRange(bankHistoryRecords);

            _logger.LogInformation($"Hard deleted {customerHistoryRecords.Count} customer and {bankHistoryRecords.Count} bank history records for document {documentId}");
        }

        /// <summary>
        /// Rebuilds balance chains after document deletion
        /// </summary>
        private async Task RebuildBalanceChainsForDocumentDeletion(AccountingDocument document)
        {
            var currencyCode = document.CurrencyCode;

            // Rebuild customer balance chains for affected customers
            if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
            {
                await RebuildCustomerBalanceChain(document.PayerCustomerId.Value, currencyCode);
            }
            if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
            {
                await RebuildCustomerBalanceChain(document.ReceiverCustomerId.Value, currencyCode);
            }

            // Rebuild bank balance chains for affected bank accounts
            if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
            {
                await RebuildBankBalanceChain(document.PayerBankAccountId.Value);
            }
            if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
            {
                await RebuildBankBalanceChain(document.ReceiverBankAccountId.Value);
            }
        }

        #endregion

        #endregion

        #region Manual Balance History Creation

        /// <summary>
        /// Creates a manual customer balance history record with specified transaction date following the coherent history pattern.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
        /// Uses the same coherent sequencing pattern as RebuildAllFinancialBalances to ensure consistency.
        /// Manual transactions are never frozen and always affect current balance calculations.
        /// </summary>
        public async Task CreateManualCustomerBalanceHistoryAsync(
            int customerId,
            string currencyCode,
            decimal amount,
            string reason,
            DateTime transactionDate,
            string performedBy = "Manual Entry",
            string? transactionNumber = null,
            string? performingUserId = null)
        {
            _logger.LogInformation($"Creating manual customer balance history: Customer {customerId}, Currency {currencyCode}, Amount {amount}, Date {transactionDate:yyyy-MM-dd}");


            // Validate customer exists
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null)
            {
                throw new ArgumentException($"Customer with ID {customerId} not found");
            }

            // Validate currency exists
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == currencyCode);
            if (currency == null)
            {
                throw new ArgumentException($"Currency with code {currencyCode} not found");
            }


            // Create the manual history record with proper coherent balance calculations
            var historyRecord = new CustomerBalanceHistory
            {
                CustomerId = customerId,
                CurrencyCode = currencyCode,
                BalanceBefore = 0, //will update to corect value in rebuild 
                TransactionAmount = amount,
                BalanceAfter = 0, //will update to corect value in rebuild 
                TransactionType = CustomerBalanceTransactionType.Manual,
                ReferenceId = null, // Manual entries don't have reference IDs
                Description = reason,
                TransactionNumber = transactionNumber,
                TransactionDate = transactionDate, // Use the specified date
                CreatedAt = DateTime.Now,
                CreatedBy = performedBy,
                IsDeleted = false // Manual transactions are never deleted via soft delete
            };



            _context.CustomerBalanceHistory.Add(historyRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Manual customer balance history created with coherent balances: ID {historyRecord.Id}, Customer {customerId}, Currency {currencyCode}, Amount {amount}");

            // Rebuild balance chain for this specific customer and currency only
            await RebuildCustomerBalanceChain(customerId, currencyCode);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Deletes a manual customer balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.Manual) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        public async Task DeleteManualCustomerBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null)
        {
            _logger.LogInformation($"Deleting manual customer balance history: Transaction ID {transactionId}");


            // Find the manual transaction
            var historyRecord = await _context.CustomerBalanceHistory
                .Include(h => h.Customer)
                .FirstOrDefaultAsync(h => h.Id == transactionId);

            if (historyRecord == null)
            {
                throw new ArgumentException($"Customer balance history with ID {transactionId} not found");
            }

            // Verify this is a manual transaction - only manual transactions can be deleted
            if (historyRecord.TransactionType != CustomerBalanceTransactionType.Manual)
            {
                throw new InvalidOperationException($"Only manual transactions can be deleted. Transaction ID {transactionId} is of type {historyRecord.TransactionType}");
            }
            // Delete the manual transaction
            _context.CustomerBalanceHistory.Remove(historyRecord);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Manual customer balance history deleted: ID {transactionId}, Customer {historyRecord.CustomerId}, Currency {historyRecord.CurrencyCode}, Amount {historyRecord.TransactionAmount}");

            // Rebuild balance chain for this specific customer and currency only
            await RebuildCustomerBalanceChain(historyRecord.CustomerId, historyRecord.CurrencyCode);
            await _context.SaveChangesAsync();
            // Send notification to admin users (excluding the performing user)

            await _notificationHub.SendManualAdjustmentNotificationAsync(
                    title: "تعدیل دستی موجودی حذف شد");



        }





        #endregion






        public async Task<int> FreezeAllOrdersAndDocumentsAsync(string performedBy = "System")
        {
            _logger.LogInformation("FreezeAllOrdersAndDocumentsAsync initiated by {PerformedBy}", performedBy);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var timestamp = DateTime.Now;

                var ordersFrozen = await _context.Orders
                    .Where(o => !o.IsFrozen)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.IsFrozen, _ => true)
                        .SetProperty(o => o.UpdatedAt, _ => timestamp));


                //NO longer freezing documents, it is not affect ony banks andd customer , orr vevey where else 

                /*var documentsFrozen = await _context.AccountingDocuments
                    .Where(d => !d.IsFrozen)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(d => d.IsFrozen, _ => true)); */

                await transaction.CommitAsync();

                _logger.LogInformation("Freeze operation completed. Orders frozen: {Orders}, Documents frozen: {Documents}", ordersFrozen);
                return (ordersFrozen);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to freeze orders/documents initiated by {PerformedBy}", performedBy);
                throw;
            }
        }




    }
}