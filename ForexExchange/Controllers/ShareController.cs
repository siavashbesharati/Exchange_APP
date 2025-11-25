using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    [AllowAnonymous]
    public class ShareController : Controller
    {
        private readonly IShareableLinkService _shareableLinkService;
        private readonly ICustomerBalanceService _customerBalanceService;
        private readonly ForexDbContext _context;

        public ShareController(
            IShareableLinkService shareableLinkService,
            ICustomerBalanceService customerBalanceService,
            ForexDbContext context)
        {
            _shareableLinkService = shareableLinkService;
            _customerBalanceService = customerBalanceService;
            _context = context;
        }

        // GET: /Share/comprehensive/{token}
        [HttpGet("Share/comprehensive/{token}")]
        public async Task<IActionResult> ComprehensiveStatement(string token)
        {
            // Validate token
            var shareableLink = await _shareableLinkService.GetValidLinkAsync(token);
            if (shareableLink == null || shareableLink.LinkType != ShareableLinkType.ComprehensiveStatement)
            {
                return View("LinkExpired");
            }

            // Mark link as accessed
            await _shareableLinkService.MarkLinkAccessedAsync(token);

            // Get customer data
            var customer = shareableLink.Customer;
            
            // Get customer balances
            var balances = await _customerBalanceService.GetCustomerBalancesAsync(customer.Id);
            
            // Get customer debt/credit information (if service exists)
            CustomerDebtCredit? debtCredit = null;
            try
            {
                // This would need to be implemented if the service exists
                // debtCredit = await _customerDebtCreditService.GetCustomerDebtCreditAsync(customer.Id);
            }
            catch
            {
                // Service might not exist, that's OK
            }
            
            // Get customer statistics
            var customerStats = await GetCustomerProfileStatsAsync(customer.Id);
            
            // Get orders
            var orders = await _context.Orders
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.CustomerId == customer.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            
            // Get accounting documents (where customer is payer or receiver) - exclude FileData to prevent memory leak
            var documents = await _context.AccountingDocuments
                .Include(a => a.PayerCustomer)
                .Include(a => a.ReceiverCustomer)
                .Include(a => a.PayerBankAccount)
                .Include(a => a.ReceiverBankAccount)
                .Where(a => a.PayerCustomerId == customer.Id || a.ReceiverCustomerId == customer.Id)
                .Select(a => new AccountingDocument
                {
                    Id = a.Id,
                    Type = a.Type,
                    PayerType = a.PayerType,
                    PayerCustomerId = a.PayerCustomerId,
                    PayerBankAccountId = a.PayerBankAccountId,
                    ReceiverType = a.ReceiverType,
                    ReceiverCustomerId = a.ReceiverCustomerId,
                    ReceiverBankAccountId = a.ReceiverBankAccountId,
                    Amount = a.Amount,
                    CurrencyCode = a.CurrencyCode,
                    Title = a.Title,
                    Description = a.Description,
                    DocumentDate = a.DocumentDate,
                    CreatedAt = a.CreatedAt,
                    IsVerified = a.IsVerified,
                    VerifiedAt = a.VerifiedAt,
                    VerifiedBy = a.VerifiedBy,
                    ReferenceNumber = a.ReferenceNumber,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    // FileData is excluded to prevent memory leak
                    Notes = a.Notes,
                    IsDeleted = a.IsDeleted,
                    DeletedAt = a.DeletedAt,
                    DeletedBy = a.DeletedBy,
                    IsFrozen = a.IsFrozen,
                    PayerCustomer = a.PayerCustomer,
                    ReceiverCustomer = a.ReceiverCustomer,
                    PayerBankAccount = a.PayerBankAccount,
                    ReceiverBankAccount = a.ReceiverBankAccount
                })
                .OrderByDescending(a => a.DocumentDate)
                .ToListAsync();

            var viewModel = new CustomerComprehensiveStatementViewModel
            {
                Customer = customer,
                Balances = balances,
                CustomerDebtCredit = debtCredit,
                Stats = customerStats,
                Orders = orders,
                Documents = documents,
                StatementDate = DateTime.Now
            };

            // Add ShareableLink information to ViewBag for display
            ViewBag.ShareableLink = shareableLink;

            return View(viewModel);
        }

        // GET: /Share/transactions/{token}
        [HttpGet("Share/transactions/{token}")]
        public async Task<IActionResult> TransactionsStatement(string token)
        {
            // Validate token
            var shareableLink = await _shareableLinkService.GetValidLinkAsync(token);
            if (shareableLink == null || shareableLink.LinkType != ShareableLinkType.TransactionsStatement)
            {
                return View("LinkExpired");
            }

            // Mark link as accessed
            await _shareableLinkService.MarkLinkAccessedAsync(token);

            var customer = shareableLink.Customer;
            
            // Get all orders for this customer
            var orders = await _context.Orders
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.CustomerId == customer.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // Calculate currency pair statistics
            var currencyPairStats = orders
                .GroupBy(o => new { FromCurrency = o.FromCurrency!.Code, ToCurrency = o.ToCurrency!.Code })
                .Select(g => new CurrencyPairStatistic
                {
                    FromCurrency = g.Key.FromCurrency,
                    ToCurrency = g.Key.ToCurrency,
                    TotalTransactions = g.Count(),
                    TotalAmount = g.Sum(o => o.FromAmount),
                    AverageRate = g.Average(o => o.Rate),
                    MinRate = g.Min(o => o.Rate),
                    MaxRate = g.Max(o => o.Rate),
                    TotalValueInTargetCurrency = g.Sum(o => o.ToAmount)
                })
                .OrderByDescending(c => c.TotalValueInTargetCurrency)
                .ToList();

            // Monthly statistics for the last 12 months
            var monthlyStats = orders
                .Where(o => o.CreatedAt >= DateTime.Now.AddMonths(-12))
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                .Select(g => new MonthlyStatistic
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TransactionCount = g.Count(),
                    TotalVolume = g.Sum(o => o.ToAmount)
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            var viewModel = new CustomerTransactionsStatementViewModel
            {
                Customer = customer,
                Orders = orders,
                CurrencyPairStats = currencyPairStats,
                MonthlyStats = monthlyStats,
                StatementDate = DateTime.Now
            };

            // Add ShareableLink information to ViewBag for display
            ViewBag.ShareableLink = shareableLink;

            return View(viewModel);
        }

        // GET: /Share/error
        public IActionResult LinkExpired()
        {
            return View();
        }

        private async Task<CustomerProfileStats> GetCustomerProfileStatsAsync(int customerId)
        {
            var totalOrders = await _context.Orders.CountAsync(o => o.CustomerId == customerId);
            var totalVolume = await _context.Orders
                .Where(o => o.CustomerId == customerId)
                .SumAsync(o => o.ToAmount);
            var totalDocuments = await _context.AccountingDocuments.CountAsync(a => a.PayerCustomerId == customerId || a.ReceiverCustomerId == customerId);
            var verifiedDocuments = await _context.AccountingDocuments
                .CountAsync(a => (a.PayerCustomerId == customerId || a.ReceiverCustomerId == customerId) && a.IsVerified);

            return new CustomerProfileStats
            {
                TotalOrders = totalOrders,
                CompletedOrders = totalOrders, // Since we don't have status, assume all are completed
                PendingOrders = 0,
                TotalTransactions = totalOrders,
                CompletedTransactions = totalOrders,
                TotalAccountingDocuments = totalDocuments,
                VerifiedAccountingDocuments = verifiedDocuments,
                TotalVolumeInToman = totalVolume,
                RegistrationDays = 0 // Could calculate this from customer creation date
            };
        }
    }
}
