using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Programmer")]
    public class PoolManagementController : Controller
    {
        private readonly ICurrencyPoolService _poolService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AdminActivityService _adminActivityService;
        private readonly ILogger<PoolManagementController> _logger;
        private readonly ICentralFinancialService _centralFinancialService;

        public PoolManagementController(
            ICurrencyPoolService poolService,
            UserManager<ApplicationUser> userManager,
            AdminActivityService adminActivityService,
            ILogger<PoolManagementController> logger,
            ICentralFinancialService centralFinancialService)
        {
            _poolService = poolService;
            _userManager = userManager;
            _adminActivityService = adminActivityService;
            _logger = logger;
            _centralFinancialService = centralFinancialService;
        }

        /// <summary>
        /// Display currency pools management page
        /// صفحه مدیریت داشبورد های ارزی
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var pools = await _poolService.GetAllPoolsAsync();
                return View(pools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading currency pools management page");
                TempData["ErrorMessage"] = "خطا در بارگذاری صفحه مدیریت داشبورد ها";
                return RedirectToAction("Index", "Home");
            }
        }

       
        /// <summary>
        /// Reset pool statistics
        /// ریست کردن آمار داشبورد 
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPoolStats(int poolId, string reason)
        {
            try
            {
                var pool = await _poolService.GetPoolAsync(poolId);
                if (pool == null)
                {
                    return Json(new { success = false, message = "داشبورد  ارزی یافت نشد" });
                }

                var oldData = new
                {
                    Balance = pool.Balance,
                    TotalBought = pool.TotalBought,
                    TotalSold = pool.TotalSold
                };

                // Reset statistics
                pool.TotalBought = 0;
                pool.TotalSold = 0;
                pool.LastUpdated = DateTime.Now;
                
                var updatedPool = await _poolService.UpdatePoolDirectAsync(pool);

                // Log admin activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogPoolStatsResetAsync(
                        poolId,
                        pool.Currency?.Code ?? "Unknown",
                        oldData.TotalBought,
                        oldData.TotalSold,
                        reason ?? "Statistics reset by admin",
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown"
                    );
                }

                _logger.LogInformation($"Pool statistics reset: {pool.Currency?.Code} by {currentUser?.UserName}");

                return Json(new { 
                    success = true, 
                    message = $"آمار {pool.Currency?.PersianName} با موفقیت ریست شد",
                    lastUpdated = pool.LastUpdated.ToString("yyyy/MM/dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting pool statistics for pool ID {poolId}");
                return Json(new { success = false, message = "خطا در ریست کردن آمار داشبورد " });
            }
        }

        /// <summary>
        /// Get pool details for modal
        /// دریافت جزئیات داشبورد  برای مودال
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPoolDetails(int poolId)
        {
            try
            {
                var pool = await _poolService.GetPoolAsync(poolId);
                if (pool == null)
                {
                    return Json(new { success = false, message = "داشبورد  ارزی یافت نشد" });
                }

                return Json(new { 
                    success = true,
                    pool = new {
                        id = pool.Id,
                        currencyId = pool.CurrencyId,
                        currencyCode = pool.Currency?.Code ?? pool.CurrencyCode, // Display from navigation
                        currencyName = pool.Currency?.PersianName,
                        balance = pool.Balance,
                        totalBought = pool.TotalBought,
                        totalSold = pool.TotalSold,
                        riskLevel = pool.RiskLevel.ToString(),
                        lastUpdated = pool.LastUpdated.ToString("yyyy/MM/dd HH:mm:ss"),
                        activeBuyOrders = pool.ActiveBuyOrderCount,
                        activeSellOrders = pool.ActiveSellOrderCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting pool details for pool ID {poolId}");
                return Json(new { success = false, message = "خطا در دریافت جزئیات داشبورد " });
            }
        }

        /// <summary>
        /// Get all currency pools for footer display
        /// دریافت تمام داشبورد های ارزی برای نمایش در فوتر
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllPoolsForFooter()
        {
            try
            {
                var pools = await _poolService.GetAllPoolsAsync();
                var poolData = pools.Select(p => new {
                    currencyId = p.CurrencyId,
                    currencyCode = p.Currency?.Code ?? p.CurrencyCode, // Display from navigation
                    balance = p.Balance
                }).ToList();

                return Json(new { success = true, pools = poolData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting currency pools for footer");
                return Json(new { success = false, message = "خطا در دریافت داده‌های داشبورد ارزی" });
            }
        }
    }
}
