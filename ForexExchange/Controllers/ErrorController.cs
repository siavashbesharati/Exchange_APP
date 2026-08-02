using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;

namespace ForexExchange.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error/{statusCode:int}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
            => RenderStatus(Normalize(statusCode));

        [Route("Error")]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            if (exceptionFeature != null)
            {
                _logger.LogError(exceptionFeature.Error,
                    "Unhandled exception occurred - {RequestPath}",
                    HttpContext.Request.Path);
            }

            return RenderStatus(500);
        }

        [Route("Error/NotFound")]
        [Route("NotFound")]
        public new IActionResult NotFound() => RenderStatus(404);

        [Route("Error/AccessDenied")]
        [Route("AccessDenied")]
        public IActionResult AccessDenied(string? returnUrl = null)
            => RenderStatus(403, returnUrl);

        [Route("Error/ServerError")]
        [Route("ServerError")]
        public IActionResult ServerError() => RenderStatus(500);

        private static int Normalize(int statusCode) => statusCode switch
        {
            401 or 403 => 403,
            500 or 502 or 503 => 500,
            _ => 404
        };

        private IActionResult RenderStatus(int statusCode, string? returnUrl = null)
        {
            Response.StatusCode = statusCode;

            var path = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
                       ?? returnUrl
                       ?? HttpContext.Request.Path.Value;

            if (statusCode == 403)
                _logger.LogWarning("403 Access denied - {RequestPath} - User: {User}",
                    path, User?.Identity?.Name ?? "Anonymous");
            else if (statusCode == 500)
                _logger.LogError("500 Internal server error - {RequestPath}", path);
            else
                _logger.LogWarning("404 Not found - {RequestPath}", path);

            var isAuthenticated = User?.Identity?.IsAuthenticated == true;

            ViewData["StatusCode"] = statusCode;
            ViewData["ReturnUrl"] = returnUrl ?? path;
            ViewData["IsAuthenticated"] = isAuthenticated;
            ViewData["UserName"] = User?.Identity?.Name;

            (ViewData["Badge"], ViewData["Title"], ViewData["Message"], ViewData["Hint"], ViewData["Theme"]) =
                statusCode switch
                {
                    403 => (
                        "خطای دسترسی · 403",
                        "اجازه ورود به این بخش را ندارید",
                        isAuthenticated
                            ? "حساب شما وارد سیستم شده، اما برای این صفحه مجوز لازم را ندارد. در صورت نیاز با مدیر سیستم هماهنگ کنید."
                            : "برای مشاهده این بخش باید وارد شوید یا با حسابی که دسترسی دارد لاگین کنید.",
                        "اگر تازه نقش یا دسترسی شما تغییر کرده، یک‌بار خارج شوید و دوباره وارد شوید.",
                        "danger"
                    ),
                    500 => (
                        "خطای سرور · 500",
                        "خطای داخلی سرور",
                        "متأسفانه مشکلی در سرور رخ داده است و نمی‌توانیم درخواست شما را پردازش کنیم. لطفاً چند لحظه بعد دوباره تلاش کنید.",
                        "اگر مشکل ادامه داشت با پشتیبانی تماس بگیرید.",
                        "warn"
                    ),
                    _ => (
                        "صفحه یافت نشد · 404",
                        "صفحه مورد نظر پیدا نشد",
                        "متأسفانه صفحه‌ای که به دنبال آن هستید وجود ندارد یا ممکن است منتقل شده باشد. لطفاً آدرس را بررسی کنید.",
                        "از منوی اصلی مسیر درست را انتخاب کنید یا به داشبورد برگردید.",
                        "muted"
                    )
                };

            return View("Status");
        }
    }
}
