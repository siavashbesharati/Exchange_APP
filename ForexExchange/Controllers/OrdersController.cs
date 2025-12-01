using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ForexExchange.Services;
using ForexExchange.Models;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Services.Notifications;
using ForexExchange.Extensions;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Operator,Programmer")]
    public class OrdersController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly ICurrencyPoolService _poolService;
        private readonly AdminActivityService _adminActivityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICustomerBalanceService _customerBalanceService;
        private readonly INotificationHub _notificationHub;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly IOrderDataService _orderDataService;

        public OrdersController(
            ForexDbContext context,
            ILogger<OrdersController> logger,
            ICurrencyPoolService poolService,
            AdminActivityService adminActivityService,
            UserManager<ApplicationUser> userManager,
            ICustomerBalanceService customerBalanceService,
            INotificationHub notificationHub,
            ICentralFinancialService centralFinancialService,

            IOrderDataService orderDataService)
        {
            _context = context;
            _logger = logger;
            _poolService = poolService;
            _adminActivityService = adminActivityService;
            _userManager = userManager;
            _customerBalanceService = customerBalanceService;
            _notificationHub = notificationHub;
            _centralFinancialService = centralFinancialService;
            _orderDataService = orderDataService;
        }

        // POST: Orders/PreviewOrderEffects
        [HttpPost]
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public async Task<IActionResult> PreviewOrderEffects([FromBody] OrderFormDataDto dto)
        {
            // Use shared order data service for consistent validation and preparation
            var orderResult = await _orderDataService.PrepareOrderFromFormDataAsync(dto);

            if (!orderResult.IsSuccess)
                return BadRequest(orderResult.ErrorMessage);

            // Use the prepared order for preview (same logic as Create method)
            var effects = await _centralFinancialService.PreviewOrderEffectsAsync(orderResult.Order!);

            // Add currency IDs and codes for client display
            var result = new
            {
                effects.CustomerId,
                effects.FromCurrencyId,
                effects.ToCurrencyId,
                effects.FromCurrencyCode,
                effects.ToCurrencyCode,
                effects.OrderFromAmount,
                effects.OrderToAmount,
                effects.OldCustomerBalanceFrom,
                effects.OldCustomerBalanceTo,
                effects.NewCustomerBalanceFrom,
                effects.NewCustomerBalanceTo,
                effects.OldPoolBalanceFrom,
                effects.OldPoolBalanceTo,
                effects.NewPoolBalanceFrom,
                effects.NewPoolBalanceTo,
               
            };
            return Json(result);
        }


        // GET: Orders
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString,
            string currencyFilter, string statusFilter, string customerFilter, int? customerIdFilter,
            string orderIdFilter, string fromCurrencyFilter, string toCurrencyFilter,
            decimal? minAmountFilter, decimal? maxAmountFilter, 
            DateTime? fromDateFilter, DateTime? toDateFilter, int? page)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = String.IsNullOrEmpty(sortOrder) ? "id_desc" : "";
            ViewData["CustomerSortParm"] = sortOrder == "Customer" ? "customer_desc" : "Customer";
            // Removed OrderType sorting
            ViewData["CurrencySortParm"] = sortOrder == "Currency" ? "currency_desc" : "Currency";
            ViewData["AmountSortParm"] = sortOrder == "Amount" ? "amount_desc" : "Amount";
            ViewData["RateSortParm"] = sortOrder == "Rate" ? "rate_desc" : "Rate";
            ViewData["StatusSortParm"] = sortOrder == "Status" ? "status_desc" : "Status";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;
            // Removed OrderType filter
            ViewData["CurrencyFilter"] = currencyFilter;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["CustomerFilter"] = customerFilter;
            ViewData["CustomerIdFilter"] = customerIdFilter;
            ViewData["OrderIdFilter"] = orderIdFilter;
            ViewData["FromCurrencyFilter"] = fromCurrencyFilter;
            ViewData["ToCurrencyFilter"] = toCurrencyFilter;
            ViewData["MinAmountFilter"] = minAmountFilter;
            ViewData["MaxAmountFilter"] = maxAmountFilter;
            ViewData["FromDateFilter"] = fromDateFilter?.ToString("yyyy-MM-dd");
            ViewData["ToDateFilter"] = toDateFilter?.ToString("yyyy-MM-dd");

            // Load currencies for dropdown filters
            ViewBag.Currencies = await _context.Currencies.OrderBy(c => c.Code).ToListAsync();
            
            // Load customers for dropdown filters ordered by FullName (exclude system customers)
            ViewBag.Customers = await _context.Customers
                .Where(c => !c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();


            IQueryable<Order> ordersQuery;


            ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency);



            // Apply filtering only - no sorting at database level for decimal fields
            if (!String.IsNullOrEmpty(currentFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName.Contains(currentFilter));
            }

            if (!String.IsNullOrEmpty(customerFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName == customerFilter);
            }

            // New filter: Customer ID (for dropdown selection)
            if (customerIdFilter.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.CustomerId == customerIdFilter.Value);
            }

            // Removed OrderType filtering

            if (!String.IsNullOrEmpty(currencyFilter))
            {
                // Try to parse currency filter as currency ID
                if (int.TryParse(currencyFilter, out var currencyId))
                {
                    ordersQuery = ordersQuery.Where(o => o.FromCurrencyId == currencyId || o.ToCurrencyId == currencyId);
                }
                else
                {
                    // Fallback: try to find currency by code (for backward compatibility)
                    var currencyByCode = await _context.Currencies
                        .FirstOrDefaultAsync(c => c.Code == currencyFilter);
                    if (currencyByCode != null)
                    {
                        ordersQuery = ordersQuery.Where(o => o.FromCurrencyId == currencyByCode.Id || o.ToCurrencyId == currencyByCode.Id);
                    }
                }
            }

            // New filter: Order ID
            if (!String.IsNullOrEmpty(orderIdFilter))
            {
                if (int.TryParse(orderIdFilter, out var orderId))
                {
                    ordersQuery = ordersQuery.Where(o => o.Id == orderId);
                }
            }

            // New filter: From Currency
            if (!String.IsNullOrEmpty(fromCurrencyFilter))
            {
                if (int.TryParse(fromCurrencyFilter, out var fromCurrencyId))
                {
                    ordersQuery = ordersQuery.Where(o => o.FromCurrencyId == fromCurrencyId);
                }
                else
                {
                    // Fallback: try to find currency by code
                    var currencyByCode = await _context.Currencies
                        .FirstOrDefaultAsync(c => c.Code == fromCurrencyFilter);
                    if (currencyByCode != null)
                    {
                        ordersQuery = ordersQuery.Where(o => o.FromCurrencyId == currencyByCode.Id);
                    }
                }
            }

            // New filter: To Currency
            if (!String.IsNullOrEmpty(toCurrencyFilter))
            {
                if (int.TryParse(toCurrencyFilter, out var toCurrencyId))
                {
                    ordersQuery = ordersQuery.Where(o => o.ToCurrencyId == toCurrencyId);
                }
                else
                {
                    // Fallback: try to find currency by code
                    var currencyByCode = await _context.Currencies
                        .FirstOrDefaultAsync(c => c.Code == toCurrencyFilter);
                    if (currencyByCode != null)
                    {
                        ordersQuery = ordersQuery.Where(o => o.ToCurrencyId == currencyByCode.Id);
                    }
                }
            }

            // New filter: Minimum Amount
            if (minAmountFilter.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.FromAmount >= minAmountFilter.Value);
            }

            // New filter: Maximum Amount
            if (maxAmountFilter.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.FromAmount <= maxAmountFilter.Value);
            }

            // New filter: From Date
            if (fromDateFilter.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.CreatedAt.Date >= fromDateFilter.Value.Date);
            }

            // New filter: To Date
            if (toDateFilter.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.CreatedAt.Date <= toDateFilter.Value.Date);
            }

            // Apply database-level sorting only for non-decimal fields
            // Load data first, then apply all sorting client-side
            List<Order> orders;

            // For non-decimal fields, we can sort at database level for better performance
            if (sortOrder?.Contains("Amount") == true || sortOrder?.Contains("Rate") == true)
            {
                // Load all data first for decimal sorting
                orders = await ordersQuery.ToListAsync();
            }
            else
            {
                // Apply non-decimal sorting at database level
                switch (sortOrder)
                {
                    case "id_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.Id);
                        break;
                    case "Customer":
                        ordersQuery = ordersQuery.OrderBy(o => o.Customer.FullName);
                        break;
                    case "customer_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.Customer.FullName);
                        break;
                    // Removed OrderType sorting
                    case "Currency":
                        ordersQuery = ordersQuery.OrderBy(o => o.FromCurrency.Code).ThenBy(o => o.ToCurrency.Code);
                        break;
                    case "currency_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.FromCurrency.Code).ThenByDescending(o => o.ToCurrency.Code);
                        break;
                    case "Date":
                        ordersQuery = ordersQuery.OrderBy(o => o.CreatedAt);
                        break;
                    case "date_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.CreatedAt);
                        break;
                    default:
                        ordersQuery = ordersQuery.OrderByDescending(o => o.CreatedAt);
                        break;
                }
                orders = await ordersQuery.ToListAsync();
            }

            // Apply client-side sorting for decimal fields
            switch (sortOrder)
            {
                case "Amount":
                    orders = orders.OrderBy(o => o.FromAmount).ToList();
                    break;
                case "amount_desc":
                    orders = orders.OrderByDescending(o => o.FromAmount).ToList();
                    break;
                case "Rate":
                    orders = orders.OrderBy(o => o.Rate).ToList();
                    break;
                case "rate_desc":
                    orders = orders.OrderByDescending(o => o.Rate).ToList();
                    break;
            }

            // Pagination
            int pageSize = 15; // 15 items per page
            int pageNumber = (page ?? 1);

            // Get total count
            int totalItems = orders.Count;

            // Apply pagination
            var pagedOrders = orders
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Pass pagination info to view
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.HasPreviousPage = pageNumber > 1;
            ViewBag.HasNextPage = pageNumber < ViewBag.TotalPages;

            return View(pagedOrders);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Debug: Log if currencies are missing
            if (order.FromCurrency == null)
            {
                _logger.LogWarning($"Order {id} has missing FromCurrency (FromCurrencyId: {order.FromCurrencyId})");
            }
            if (order.ToCurrency == null)
            {
                _logger.LogWarning($"Order {id} has missing ToCurrency (ToCurrencyId: {order.ToCurrencyId})");
            }

            return View(order);
        }

        // GET: Orders/GetOrderDetails/5 (for AJAX popup)
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.Id == id)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return Json(new { error = "سفارش یافت نشد" });
                }

                var result = new
                {
                    id = order.Id,
                    customerId = order.CustomerId,
                    customerName = order.Customer?.FullName ?? "نامشخص",
                    fromCurrencyName = order.FromCurrency?.Name ?? "نامشخص",
                    toCurrencyName = order.ToCurrency?.Name ?? "نامشخص",
                    fromAmount = order.FromAmount,
                    toAmount = order.ToAmount,
                    exchangeRate = order.Rate,
                    createdAt = order.CreatedAt,
                    updatedAt = order.UpdatedAt
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order details for ID: {OrderId}", id);
                return Json(new { error = "خطا در دریافت جزئیات سفارش" });
            }
        }

        // GET: Orders/Create
        public async Task<IActionResult> Create(int? customerId = null)
        {
            // Load only essential data with minimal queries
            await LoadCreateViewDataOptimized();

            // If customerId is provided, create an Order model with that customer pre-selected
            if (customerId.HasValue)
            {
                var order = new Order
                {
                    CustomerId = customerId.Value
                };
                return View(order);
            }

            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody]Order order)
        {
            // Debug: Log received order data first
            _logger.LogInformation($"Order data received - CustomerId: {order.CustomerId}, FromCurrencyId: {order.FromCurrencyId}, ToCurrencyId: {order.ToCurrencyId}, FromAmount: {order.FromAmount}, ToAmount: {order.ToAmount}, Rate: {order.Rate}");

            // Use shared order data service for consistent validation and preparation
            var dto = new OrderFormDataDto
            {
                CustomerId = order.CustomerId,
                FromCurrencyId = order.FromCurrencyId,
                ToCurrencyId = order.ToCurrencyId,
                FromAmount = order.FromAmount,
                ToAmount = order.ToAmount,
                Rate = order.Rate,
                CreatedAt = order.CreatedAt,
                Notes = order.Notes
            };

            var orderResult = await _orderDataService.PrepareOrderFromFormDataAsync(dto);

            if (!orderResult.IsSuccess)
            {
                ModelState.AddModelError("", orderResult.ErrorMessage!);
            }

            // Use the validated and prepared order from the service
            order = orderResult.Order!;
            var fromCurrency = orderResult.FromCurrency!;
            var toCurrency = orderResult.ToCurrency!;

            // Remove Customer navigation property from validation as we only need CustomerId
            ModelState.Remove("Customer");
            ModelState.Remove("Transactions");
            ModelState.Remove("Receipts");
            ModelState.Remove("FromCurrency");
            ModelState.Remove("ToCurrency");
            ModelState.Remove("TotalAmount"); // TotalAmount is calculated server-side



            if (ModelState.IsValid)
            {
                try
                {
                    // Update customer balances and currency pools for the order
                    await _centralFinancialService.ProcessOrderCreationAsync(order);

                    // Load related entities for notification
                    await _context.Entry(order).Reference(o => o.Customer).LoadAsync();
                    await _context.Entry(order).Reference(o => o.FromCurrency).LoadAsync();
                    await _context.Entry(order).Reference(o => o.ToCurrency).LoadAsync();

                    _logger.LogInformation("Completed ProcessOrderCreationAsync for Order {OrderId}", order.Id);
                    _logger.LogInformation($"Order currency : {order.FromCurrency.PersianName}");
                    
                    // Log admin activity and send notifications
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogOrderCreatedAsync(order, currentUser.Id, currentUser.UserName ?? "Unknown");
                        
                        // Send notifications through central hub (replaces individual notification calls)
                        await _notificationHub.SendOrderNotificationAsync(order, NotificationEventType.OrderCreated, currentUser.Id);
                    }

                    _logger.LogInformation($"Order created successfully - Id: {order.Id}, Rate: {order.Rate} , Total: {order.ToAmount}");

                    return Json(new { success = true, message = "سفارش با موفقیت ثبت شد و موجودی‌ها بازمحاسبه شدند", redirectUrl = Url.Action(nameof(Details), new { id = order.Id }) });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error creating order: {ex.Message}");
                    return Json(new { success = false, message = $"خطا در ثبت سفارش: {ex.Message}" });
                }
            }
            else
            {
                return Json(new { success = false, message =  "خطایی در ثبت معمامله بوجود آمد" });
            }

        }




        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }



        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        private async Task LoadCreateViewDataOptimized()
        {
            // Load minimal currency data for dropdowns (just ID, Code, Name)
            var currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Code, c.Name, c.DisplayOrder, c.RatePriority })
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            // Create SelectListItem for proper binding
            ViewBag.FromCurrencies = currencies.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Code} - {c.Name}"
            }).ToList();

            ViewBag.ToCurrencies = currencies.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Code} - {c.Name}"
            }).ToList();

            // Pass currency data with RatePriority to JavaScript
            ViewBag.CurrenciesData = currencies.ToDictionary(c => c.Id, c => new { c.Code, c.Name, c.RatePriority });

            // Load minimal customer data for dropdown (just ID and FullName) - exclude system customers
            var customers = _context.Customers
                .Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName);

            ViewBag.Customers = customers.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.FullName
            }).ToList();

            ViewBag.IsAdminOrStaff = true;

            // Simplified: load a dictionary mapping currency ID to balance
            var pools = await _poolService.GetAllPoolsAsync();
            // Use CurrencyId for grouping, CurrencyCode for display
            var poolDict = pools
                .Where(p => p.CurrencyId > 0)
                .GroupBy(p => p.CurrencyId)
                .ToDictionary(g => g.First().Currency?.Code ?? g.First().CurrencyCode ?? "UNKNOWN", g => g.Sum(p => p.Balance));
            ViewBag.PoolData = poolDict;


        }


        // AJAX endpoint to get customers list
        [HttpGet]
        public async Task<IActionResult> GetCustomers(string search = "")
        {
            try
            {
                var query = _context.Customers.Where(c => c.IsActive && !c.IsSystem);

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(c => c.FullName.Contains(search));
                }

                var customers = await query
                    .Select(c => new { c.Id, c.FullName })
                    .OrderBy(c => c.FullName)
                    .Take(50) // Limit results to prevent large responses
                    .ToListAsync();

                return Json(new { success = true, customers = customers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers list");
                return Json(new { success = false, error = "خطا در دریافت لیست مشتریان" });
            }
        }



        // POST: Orders/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Programmer")] // Only admins can delete orders
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "معامله یافت نشد.";
                    return RedirectToAction(nameof(Index));
                }

                // Use centralized service to delete with proper financial impact reversal
                var currentUser = await _userManager.GetUserAsync(User);
                await _centralFinancialService.DeleteOrderAsync(order, currentUser?.UserName ?? "Admin");

                // Log admin activity
                var adminActivity = new AdminActivity
                {
                    AdminUserId = currentUser?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.OrderCancelled, // Using cancellation as closest to deletion
                    Description = $"Deleted Order #{order.Id} - {order.FromCurrency.Code} to {order.ToCurrency.Code}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString()
                };
                _context.AdminActivities.Add(adminActivity);
                await _context.SaveChangesAsync();

                // Log admin activity and send notifications
                if (currentUser != null)
                {
                    await _adminActivityService.LogOrderCancelledAsync(order, currentUser.Id, currentUser.UserName ?? "Unknown");

                    // Send notifications through central hub (replaces individual notification calls)
                    await _notificationHub.SendOrderNotificationAsync(order, NotificationEventType.OrderDeleted, currentUser.Id);
                }



                TempData["SuccessMessage"] = $"معامله #{order.Id} با موفقیت حذف شد و تأثیرات مالی آن برگردانده شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting order {id}");
                TempData["ErrorMessage"] = "خطا در حذف معامله. لطفاً دوباره تلاش کنید.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
