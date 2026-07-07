using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForexExchange.Authorization;
using ForexExchange.Extensions;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Controllers
{
    [HasPermission(Permissions.Order_View)]
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
            IOrderDataService orderDataService
        )
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
        [HasPermission(Permissions.Order_Create)]
        public async Task<IActionResult> PreviewOrderEffects([FromBody] OrderFormDataDto dto)
        {
            // Use shared order data service for consistent validation and preparation
            var orderResult = await _orderDataService.PrepareOrderFromFormDataAsync(dto);

            if (!orderResult.IsSuccess)
                return BadRequest(orderResult.ErrorMessage);

            // Use the prepared order for preview (same logic as Create method)
            var effects = await _centralFinancialService.PreviewOrderEffectsAsync(
                orderResult.Order!
            );

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
        [HasPermission(Permissions.Order_View)]
        public async Task<IActionResult> Index(
            string sortOrder,
            string currentFilter,
            string searchString,
            string currencyFilter,
            string statusFilter,
            string customerFilter,
            int? customerIdFilter,
            string orderIdFilter,
            string fromCurrencyFilter,
            string toCurrencyFilter,
            decimal? minAmountFilter,
            decimal? maxAmountFilter,
            DateTime? fromDateFilter,
            DateTime? toDateFilter,
            int? page
        )
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
            ViewBag.Customers = await _context
                .Customers.Where(c => !c.IsSystem)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            IQueryable<Order> ordersQuery;

            ordersQuery = _context
                .Orders.Include(o => o.Customer)
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
                    ordersQuery = ordersQuery.Where(o =>
                        o.FromCurrencyId == currencyId || o.ToCurrencyId == currencyId
                    );
                }
                else
                {
                    // Fallback: try to find currency by code (for backward compatibility)
                    var currencyByCode = await _context.Currencies.FirstOrDefaultAsync(c =>
                        c.Code == currencyFilter
                    );
                    if (currencyByCode != null)
                    {
                        ordersQuery = ordersQuery.Where(o =>
                            o.FromCurrencyId == currencyByCode.Id
                            || o.ToCurrencyId == currencyByCode.Id
                        );
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
                    var currencyByCode = await _context.Currencies.FirstOrDefaultAsync(c =>
                        c.Code == fromCurrencyFilter
                    );
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
                    var currencyByCode = await _context.Currencies.FirstOrDefaultAsync(c =>
                        c.Code == toCurrencyFilter
                    );
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
                        ordersQuery = ordersQuery
                            .OrderBy(o => o.FromCurrency.Code)
                            .ThenBy(o => o.ToCurrency.Code);
                        break;
                    case "currency_desc":
                        ordersQuery = ordersQuery
                            .OrderByDescending(o => o.FromCurrency.Code)
                            .ThenByDescending(o => o.ToCurrency.Code);
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
            var pagedOrders = orders.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

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
        [HasPermission(Permissions.Order_Detail)] 
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context
                .Orders.Include(o => o.Customer)
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
                _logger.LogWarning(
                    $"Order {id} has missing FromCurrency (FromCurrencyId: {order.FromCurrencyId})"
                );
            }
            if (order.ToCurrency == null)
            {
                _logger.LogWarning(
                    $"Order {id} has missing ToCurrency (ToCurrencyId: {order.ToCurrencyId})"
                );
            }

            return View(order);
        }

        // GET: Orders/GetOrderDetails/5 (for AJAX popup)
        [HttpGet]
        [HasPermission(Permissions.Order_Detail)] 
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            try
            {
                var order = await _context
                    .Orders.Include(o => o.Customer)
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
                    updatedAt = order.UpdatedAt,
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
        [HasPermission(Permissions.Order_Create)] 
        public async Task<IActionResult> Create(int? customerId = null)
        {
            // Load only essential data with minimal queries
            await LoadCreateViewDataOptimized();

            // If customerId is provided, create an Order model with that customer pre-selected
            if (customerId.HasValue)
            {
                var order = new Order { CustomerId = customerId.Value };
                return View(order);
            }

            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Order_Create)] 
        public async Task<IActionResult> Create([FromBody] Order order)
        {
            // Debug: Log received order data first
            _logger.LogInformation(
                $"Order data received - CustomerId: {order.CustomerId}, FromCurrencyId: {order.FromCurrencyId}, ToCurrencyId: {order.ToCurrencyId}, FromAmount: {order.FromAmount}, ToAmount: {order.ToAmount}, Rate: {order.Rate}"
            );

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
                Notes = order.Notes,
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

                    _logger.LogInformation(
                        "Completed ProcessOrderCreationAsync for Order {OrderId}",
                        order.Id
                    );
                    _logger.LogInformation($"Order currency : {order.FromCurrency.PersianName}");

                    // Log admin activity and send notifications
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogOrderCreatedAsync(
                            order,
                            currentUser.Id,
                            currentUser.UserName ?? "Unknown"
                        );

                        // Send notifications through central hub (replaces individual notification calls)
                        await _notificationHub.SendOrderNotificationAsync(
                            order,
                            NotificationEventType.OrderCreated,
                            currentUser.Id
                        );
                    }

                    _logger.LogInformation(
                        $"Order created successfully - Id: {order.Id}, Rate: {order.Rate} , Total: {order.ToAmount}"
                    );

                    return Json(
                        new
                        {
                            success = true,
                            message = "سفارش با موفقیت ثبت شد و موجودی‌ها بازمحاسبه شدند",
                            redirectUrl = Url.Action(nameof(Details), new { id = order.Id }),
                        }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error creating order: {ex.Message}");
                    return Json(
                        new { success = false, message = $"خطا در ثبت سفارش: {ex.Message}" }
                    );
                }
            }
            else
            {
                return Json(new { success = false, message = "خطایی در ثبت معمامله بوجود آمد" });
            }
        }

        // GET: Orders/Delete/5
        [HasPermission(Permissions.Order_Delete)] 
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context
                .Orders.Include(o => o.Customer)
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
            var currencies = await _context
                .Currencies.Where(c => c.IsActive)
                .Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.Name,
                    c.DisplayOrder,
                    c.RatePriority,
                })
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            // Create SelectListItem for proper binding
            ViewBag.FromCurrencies = currencies
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Code} - {c.Name}",
                })
                .ToList();

            ViewBag.ToCurrencies = currencies
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Code} - {c.Name}",
                })
                .ToList();

            // Pass currency data with RatePriority to JavaScript
            ViewBag.CurrenciesData = currencies.ToDictionary(
                c => c.Id,
                c => new
                {
                    c.Code,
                    c.Name,
                    c.RatePriority,
                }
            );

            // Load minimal customer data for dropdown (just ID and FullName) - exclude system customers
            var customers = _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName);

            ViewBag.Customers = customers
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.FullName,
                })
                .ToList();

            ViewBag.IsAdminOrStaff = true;

            // Simplified: load a dictionary mapping currency ID to balance
            var pools = await _poolService.GetAllPoolsAsync();
            // Use CurrencyId for grouping, CurrencyCode for display
            var poolDict = pools
                .Where(p => p.CurrencyId > 0)
                .GroupBy(p => p.CurrencyId)
                .ToDictionary(
                    g => g.First().Currency?.Code ?? g.First().CurrencyCode ?? "UNKNOWN",
                    g => g.Sum(p => p.Balance)
                );
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
        [HasPermission(Permissions.Order_Delete)] 
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var order = await _context
                    .Orders.Include(o => o.Customer)
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
                await _centralFinancialService.DeleteOrderAsync(
                    order,
                    currentUser?.UserName ?? "Admin"
                );

                // Log admin activity
                var adminActivity = new AdminActivity
                {
                    AdminUserId = currentUser?.Id ?? "Unknown",
                    ActivityType = AdminActivityType.OrderCancelled, // Using cancellation as closest to deletion
                    Description =
                        $"Deleted Order #{order.Id} - {order.FromCurrency.Code} to {order.ToCurrency.Code}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                };
                _context.AdminActivities.Add(adminActivity);
                await _context.SaveChangesAsync();

                // Log admin activity and send notifications
                if (currentUser != null)
                {
                    await _adminActivityService.LogOrderCancelledAsync(
                        order,
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown"
                    );

                    // Send notifications through central hub (replaces individual notification calls)
                    await _notificationHub.SendOrderNotificationAsync(
                        order,
                        NotificationEventType.OrderDeleted,
                        currentUser.Id
                    );
                }

                TempData["SuccessMessage"] =
                    $"معامله #{order.Id} با موفقیت حذف شد و تأثیرات مالی آن برگردانده شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting order {id}");
                TempData["ErrorMessage"] = "خطا در حذف معامله. لطفاً دوباره تلاش کنید.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// GET: Show view to upload CSV file for importing orders.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UploadOrdersCsv()
        {
            var customers = await _context
                .Customers.Where(c => c.IsActive && !c.IsSystem)
                .OrderBy(c => c.FullName)
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.FullName ?? c.Id.ToString(),
                })
                .ToListAsync();
            ViewBag.Customers = customers;
            return View();
        }

        /// <summary>
        /// Upload CSV file to import orders. Checks reference (stored in Notes as ImportRef:xxx) from database to avoid re-importing.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(10_485_760)]
        public async Task<IActionResult> UploadOrdersCsv(IFormFile csvFile, int customerId)
        {
            var importedCount = 0;
            var skippedCount = 0;
            var duplicateRefs = new List<string>();
            var errors = new List<object>();

            if (csvFile == null || csvFile.Length == 0)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "فایلی انتخاب نشده است.",
                        importedCount = 0,
                        skippedCount = 0,
                        duplicateRefs,
                        errors,
                    }
                );
            }

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "مشتری یافت نشد.",
                        importedCount = 0,
                        skippedCount = 0,
                        duplicateRefs,
                        errors,
                    }
                );
            }

            List<OrderCsvRow> rows;
            try
            {
                using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
                rows = ParseOrdersCsv(reader, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing orders CSV");
                return Json(
                    new
                    {
                        success = false,
                        message = $"خطا در خواندن فایل: {ex.Message}",
                        importedCount = 0,
                        skippedCount = 0,
                        duplicateRefs,
                        errors,
                    }
                );
            }

            if (rows.Count == 0)
            {
                return Json(
                    new
                    {
                        success = true,
                        message = "هیچ ردیف معتبری در فایل یافت نشد.",
                        importedCount = 0,
                        skippedCount = 0,
                        duplicateRefs,
                        errors,
                    }
                );
            }

            var existingRefsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordersWithImportRef = await _context
                .Orders.Where(o =>
                    o.CustomerId == customerId && o.Notes != null && o.Notes.Contains("ImportRef:")
                )
                .Select(o => o.Notes)
                .ToListAsync();
            foreach (var notes in ordersWithImportRef)
            {
                if (notes == null)
                    continue;
                var idx = notes.IndexOf("ImportRef:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    continue;
                var start = idx + "ImportRef:".Length;
                var end = notes.IndexOf(' ', start);
                if (end < 0)
                    end = notes.Length;
                var refId = notes
                    .Substring(start, Math.Min(end - start, notes.Length - start))
                    .Trim();
                if (!string.IsNullOrEmpty(refId))
                    existingRefsSet.Add(refId);
            }

            var orderGroups = rows.GroupBy(r => r.ReferenceId ?? "")
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToList();

            foreach (var group in orderGroups)
            {
                var refId = group.Key;
                if (existingRefsSet.Contains(refId))
                {
                    skippedCount++;
                    duplicateRefs.Add(refId);
                    continue;
                }

                var list = group.OrderBy(r => r.Type == "Buy" ? 0 : 1).ToList();
                var buyRow = list.FirstOrDefault(r =>
                    r.Type?.Equals("Buy", StringComparison.OrdinalIgnoreCase) == true
                );
                var sellRow = list.FirstOrDefault(r =>
                    r.Type?.Equals("Sell", StringComparison.OrdinalIgnoreCase) == true
                );

                if (buyRow == null || sellRow == null)
                {
                    errors.Add(
                        new
                        {
                            refId,
                            message = "برای هر سفارش باید یک ردیف Buy و یک ردیف Sell با همان TransactionID وجود داشته باشد.",
                        }
                    );
                    continue;
                }

                int fromCurrencyId = buyRow.CurrencyId ?? 0;
                int toCurrencyId = sellRow.CurrencyId ?? 0;
                var fromCode = buyRow.CurrencyCode;
                var toCode = sellRow.CurrencyCode;
                if (fromCurrencyId <= 0 && !string.IsNullOrWhiteSpace(fromCode))
                {
                    var c = await _context.Currencies.FirstOrDefaultAsync(cu =>
                        cu.Code != null
                        && cu.Code.Trim().ToUpperInvariant() == fromCode!.Trim().ToUpperInvariant()
                    );
                    if (c != null)
                    {
                        fromCurrencyId = c.Id;
                        fromCode = c.Code;
                    }
                }
                if (toCurrencyId <= 0 && !string.IsNullOrWhiteSpace(toCode))
                {
                    var c = await _context.Currencies.FirstOrDefaultAsync(cu =>
                        cu.Code != null
                        && cu.Code.Trim().ToUpperInvariant() == toCode!.Trim().ToUpperInvariant()
                    );
                    if (c != null)
                    {
                        toCurrencyId = c.Id;
                        toCode = c.Code;
                    }
                }
                if (fromCurrencyId <= 0 || toCurrencyId <= 0)
                {
                    errors.Add(new { refId, message = "ارز مبدا یا مقصد معتبر نیست." });
                    continue;
                }

                decimal fromAmount = Math.Abs(buyRow.Amount);
                decimal toAmount = Math.Abs(sellRow.Amount);
                if (fromAmount <= 0 || toAmount <= 0)
                {
                    errors.Add(new { refId, message = "مبلغ باید بزرگتر از صفر باشد." });
                    continue;
                }

                decimal rate = buyRow.Rate ?? (fromAmount / toAmount);
                var createdAt = buyRow.Date ?? DateTime.Today;
                var notes =
                    (buyRow.Description ?? sellRow.Description ?? "") + " ImportRef:" + refId;

                var dto = new OrderFormDataDto
                {
                    CustomerId = customerId,
                    FromCurrencyId = fromCurrencyId,
                    ToCurrencyId = toCurrencyId,
                    FromAmount = fromAmount,
                    ToAmount = toAmount,
                    Rate = rate,
                    CreatedAt = createdAt,
                    Notes = notes,
                };

                try
                {
                    var orderResult = await _orderDataService.PrepareOrderFromFormDataAsync(dto);
                    if (!orderResult.IsSuccess)
                    {
                        errors.Add(
                            new
                            {
                                refId,
                                message = orderResult.ErrorMessage ?? "خطا در آماده‌سازی سفارش.",
                            }
                        );
                        continue;
                    }
                    var order = orderResult.Order!;
                    order.Notes = notes;
                    await _centralFinancialService.ProcessOrderCreationAsync(order, "CSV Import");
                    importedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating order for ref {RefId}", refId);
                    errors.Add(new { refId, message = ex.Message });
                }
            }

            return Json(
                new
                {
                    success = true,
                    message = $"واردات انجام شد. تعداد: {importedCount}، رد شده: {skippedCount}",
                    importedCount,
                    skippedCount,
                    duplicateRefs,
                    errors,
                }
            );
        }

        private static List<OrderCsvRow> ParseOrdersCsv(StreamReader reader, List<object> errors)
        {
            var rows = new List<OrderCsvRow>();
            var lineNum = 0;
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return rows;
            lineNum++;
            var headers = headerLine!.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            int idxDate = Array.FindIndex(headers, h => h == "date");
            int idxType = Array.FindIndex(headers, h => h == "type");
            int idxCurrencyId = Array.FindIndex(headers, h => h == "currencyid");
            int idxCurrencyCode = Array.FindIndex(headers, h => h == "currencycode");
            int idxCurrency = Array.FindIndex(headers, h => h == "currency");
            int idxTransactionId = Array.FindIndex(headers, h => h == "transactionid");
            int idxId = Array.FindIndex(headers, h => h == "id");
            int idxAmount = Array.FindIndex(headers, h => h == "amount");
            int idxAmountIrr = Array.FindIndex(
                headers,
                h => h.Contains("amount") && h.Contains("irr")
            );
            int idxDesc = Array.FindIndex(headers, h => h == "description");
            int idxNote = Array.FindIndex(headers, h => h == "note");

            int refCol = idxTransactionId >= 0 ? idxTransactionId : idxId;
            int amountCol = idxAmount >= 0 ? idxAmount : idxAmountIrr;
            if (idxDate < 0 || idxType < 0 || refCol < 0 || amountCol < 0)
            {
                errors.Add(
                    new
                    {
                        line = lineNum,
                        message = "ستون‌های ضروری (Date, Type, Reference, Amount) یافت نشد.",
                    }
                );
                return rows;
            }

            while (reader.ReadLine() is { } line)
            {
                lineNum++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var parts = SplitCsvLine(line);
                if (parts.Count <= Math.Max(refCol, amountCol))
                    continue;
                var refId = refCol < parts.Count ? NormalizeRefId(parts[refCol]) : "";
                if (string.IsNullOrWhiteSpace(refId))
                    continue;
                DateTime? date = null;
                if (
                    idxDate >= 0
                    && idxDate < parts.Count
                    && DateTime.TryParse(
                        parts[idxDate],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var d
                    )
                )
                    date = d;
                var type = (idxType >= 0 && idxType < parts.Count) ? parts[idxType] : null;
                int? currencyId = null;
                if (
                    idxCurrencyId >= 0
                    && idxCurrencyId < parts.Count
                    && int.TryParse(
                        parts[idxCurrencyId],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var cid
                    )
                )
                    currencyId = cid;
                var currencyCode =
                    (idxCurrencyCode >= 0 && idxCurrencyCode < parts.Count)
                        ? parts[idxCurrencyCode]
                        : (
                            (idxCurrency >= 0 && idxCurrency < parts.Count)
                                ? parts[idxCurrency]
                                : null
                        );
                var amountStr =
                    (amountCol >= 0 && amountCol < parts.Count)
                        ? parts[amountCol].Replace(",", "", StringComparison.Ordinal)
                        : "0";
                if (
                    !decimal.TryParse(
                        amountStr,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var amt
                    )
                )
                    continue;
                var desc =
                    (idxDesc >= 0 && idxDesc < parts.Count)
                        ? parts[idxDesc]
                        : ((idxNote >= 0 && idxNote < parts.Count) ? parts[idxNote] : null);
                decimal? rate = null;
                if (desc != null)
                {
                    var rateMatch = Regex.Match(
                        desc,
                        @"Rate:\s*([\d,\.]+)",
                        RegexOptions.IgnoreCase
                    );
                    if (
                        rateMatch.Success
                        && decimal.TryParse(
                            rateMatch.Groups[1].Value.Replace(",", "", StringComparison.Ordinal),
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var r
                        )
                    )
                        rate = r;
                }
                rows.Add(
                    new OrderCsvRow
                    {
                        ReferenceId = refId,
                        Date = date,
                        Type = type,
                        CurrencyId = currencyId,
                        CurrencyCode = currencyCode?.Trim(),
                        Amount = Math.Abs(amt),
                        Description = desc,
                        Rate = rate,
                    }
                );
            }
            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && c == ',')
                {
                    list.Add(sb.ToString().Trim());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            list.Add(sb.ToString().Trim());
            return list;
        }

        private static string NormalizeRefId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Trim();
        }

        private sealed class OrderCsvRow
        {
            public string? ReferenceId { get; set; }
            public DateTime? Date { get; set; }
            public string? Type { get; set; }
            public int? CurrencyId { get; set; }
            public string? CurrencyCode { get; set; }
            public decimal Amount { get; set; }
            public string? Description { get; set; }
            public decimal? Rate { get; set; }
        }
    }
}
