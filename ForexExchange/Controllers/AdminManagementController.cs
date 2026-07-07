using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ForexExchange.Authorization; // Add for custom permissions

namespace ForexExchange.Controllers
{
    /// <summary>
    /// Admin Management Controller
    /// کنترلر مدیریت ادمین
    /// </summary>
    [Authorize(Roles = "Admin,Programmer")]
    public class AdminManagementController : Controller
    {
        private readonly AdminActivityService _adminActivityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ForexDbContext _context;
        private readonly ITotpService _totpService;
        private readonly IPermissionService _permissionService;

        public AdminManagementController(
            AdminActivityService adminActivityService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context,
            ITotpService totpService,
            IPermissionService permissionService)
        {
            _adminActivityService = adminActivityService;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _totpService = totpService;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Admin Activity Log Index
        /// صفحه اصلی لاگ فعالیت‌های ادمین
        /// </summary>
        public async Task<IActionResult> Index(
            string? adminUserId = null,
            AdminActivityType? activityType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 50)
        {
            // Get current user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            // Get activities (all admins can see all activities)
            // First, get total count for pagination
            var totalActivities = await _adminActivityService.GetAllActivitiesAsync(
                adminUserId, activityType, fromDate, toDate);
            var totalCount = totalActivities.Count;

            // Then get the paginated activities
            var activities = await _adminActivityService.GetAllActivitiesAsync(
                adminUserId, activityType, fromDate, toDate, pageSize * 50); // Get enough data
            var paginatedActivities = activities.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Get user full names for activities
            var userIds = paginatedActivities.Select(a => a.AdminUserId).Distinct().ToList();
            var users = await _context.Users.Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? "نامشخص");

            // Create a dictionary to map AdminUserId to FullName for activities
            ViewBag.UserFullNames = users;

            // Get all admin users for filter dropdown
            var adminUsers = new List<ApplicationUser>();
            var adminRole = await _roleManager.FindByNameAsync("Admin");

            if (adminRole != null)
            {
                var adminUserIds = _context.UserRoles
                    .Where(ur => ur.RoleId == adminRole.Id)
                    .Select(ur => ur.UserId)
                    .Distinct();
                adminUsers = await _context.Users
                    .Where(u => adminUserIds.Contains(u.Id))
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
            }

            // Get activity statistics
            var stats = await _adminActivityService.GetActivityStatisticsAsync(fromDate, toDate);

            ViewBag.CurrentUser = currentUser;
            ViewBag.IsSuperAdmin = true; // All admins have full access now
            ViewBag.AdminUsers = adminUsers;
            ViewBag.Activities = paginatedActivities;
            ViewBag.TotalActivities = totalCount;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.ActivityStats = stats;
            ViewBag.FilterAdminUserId = adminUserId;
            ViewBag.FilterActivityType = activityType;
            ViewBag.FilterFromDate = fromDate;
            ViewBag.FilterToDate = toDate;

            return View();
        }

        /// <summary>
        /// Get activity details
        /// دریافت جزئیات فعالیت
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActivityDetails(int id)
        {
            var activity = await _context.AdminActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound();

            // Check permissions (all admins can see all activity details)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Forbid();

            return Json(new
            {
                activity.Id,
                activity.AdminUserId,
                activity.AdminUsername,
                activity.ActivityType,
                activity.Description,
                activity.Details,
                activity.EntityType,
                activity.EntityId,
                activity.OldValue,
                activity.NewValue,
                activity.IsSuccess,
                activity.Timestamp,
                activity.IpAddress,
                activity.UserAgent
            });
        }

        /// <summary>
        /// Admin Dashboard
        /// داشبورد ادمین
        /// </summary>
        public async Task<IActionResult> Dashboard()
        {
            // Get system statistics
            var totalUsers = await _userManager.Users.CountAsync();
            var totalAdmins = 0;

            var adminRole = await _roleManager.FindByNameAsync("Admin");

            if (adminRole != null)
            {
                totalAdmins = _context.UserRoles.Count(ur => ur.RoleId == adminRole.Id);
            }

            // Get recent activities
            var recentActivities = await _adminActivityService.GetAllActivitiesAsync(limit: 10);

            // Get activity statistics for last 30 days
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var monthlyStats = await _adminActivityService.GetActivityStatisticsAsync(thirtyDaysAgo);

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalAdmins = totalAdmins;
            ViewBag.TotalSuperAdmins = 0; // No SuperAdmin role exists
            ViewBag.RecentActivities = recentActivities;
            ViewBag.MonthlyStats = monthlyStats;

            return View();
        }

        /// <summary>
        /// Manage Admin Users
        /// مدیریت کاربران ادمین
        /// </summary>
        public async Task<IActionResult> ManageAdmins()
        {
            // Get current user to check their role
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get all administrative roles (Admin, Programmer, Operator)
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var programmerRole = await _roleManager.FindByNameAsync("Programmer");
            var operatorRole = await _roleManager.FindByNameAsync("Operator");

            if (adminRole == null)
            {
                TempData["Error"] = "نقش ادمین یافت نشد.";
                return RedirectToAction("Dashboard");
            }

            // Get user IDs for administrative roles
            var roleIds = new List<string> { adminRole.Id };
            if (programmerRole != null) roleIds.Add(programmerRole.Id);
            if (operatorRole != null) roleIds.Add(operatorRole.Id);

            var adminUserIds = _context.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct();

            var adminUsers = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .ToListAsync();

            // If current user is not a Programmer, filter out Programmer users
            if (currentUser.Role != UserRole.Programmer)
            {
                adminUsers = adminUsers.Where(u => u.Role != UserRole.Programmer).ToList();
            }

            // Pass current user role to view for additional filtering logic
            ViewBag.CurrentUserRole = currentUser.Role;

            return View(adminUsers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Users_RegenerateTotpSecret)]
        public async Task<IActionResult> RegenerateTotpSecret(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "شناسه کاربر نامعتبر است.";
                return RedirectToAction("ManageAdmins");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null)
            {
                TempData["Error"] = "برای انجام این عملیات ابتدا وارد شوید.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "کاربر مورد نظر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            var oldSecretExists = !string.IsNullOrWhiteSpace(user.TotpSecret);
            user.TotpSecret = _totpService.GenerateSecret();
            user.TotpSecretUpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "بروزرسانی کلید یکبارمصرف با خطا مواجه شد.";
                return RedirectToAction("ManageAdmins");
            }

            await _adminActivityService.LogActivityAsync(
                currentAdmin.Id,
                currentAdmin.UserName ?? currentAdmin.PhoneNumber ?? "admin",
                AdminActivityType.UserUpdated,
                $"کلید یکبارمصرف کاربر {user.UserName} {(oldSecretExists ? "بازتولید" : "ایجاد")} شد.",
                entityType: "ApplicationUser",
                entityId: null,
                oldValue: oldSecretExists ? "***" : null,
                newValue: "***"
            );

            TempData["Success"] = $"کلید یکبارمصرف جدید برای کاربر {user.FullName ?? user.UserName} ایجاد شد.";
            return RedirectToAction("ManageAdmins");
        }

        [HttpPost]
        [Authorize(Roles = "Programmer")]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Users_ResetAllSessions)]
        public async Task<IActionResult> ResetAllSessions()
        {
            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null)
            {
                TempData["Error"] = "برای انجام این عملیات ابتدا وارد شوید.";
                return RedirectToAction("Login", "Account");
            }

            var users = await _userManager.Users.ToListAsync();
            foreach (var user in users)
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }

            await _adminActivityService.LogActivityAsync(
                currentAdmin.Id,
                currentAdmin.UserName ?? currentAdmin.PhoneNumber ?? "admin",
                AdminActivityType.UserUpdated,
                "نشست همهٔ کاربران ریست شد.",
                entityType: "ApplicationUser",
                newValue: "***"
            );

            TempData["Success"] = "تمام کاربران برای ورود مجدد نیازمند احراز هویت خواهند بود.";
            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Create New Admin User
        /// ایجاد کاربر ادمین جدید
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Users_Create)]
        public async Task<IActionResult> CreateAdmin(string userName, string email, string password, UserRole role, string fullName)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fullName))
            {
                TempData["Error"] = "نام و نام خانوادگی، شماره تلفن و رمز عبور الزامی هستند.";
                return RedirectToAction("ManageAdmins");
            }

            // Normalize phone number
            string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(userName);

            // Validate normalized phone number
            if (!PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber))
            {
                TempData["Error"] = "فرمت شماره تلفن صحیح نیست. لطفاً شماره تلفن معتبر وارد کنید.";
                return RedirectToAction("ManageAdmins");
            }

            // Check if normalized phone number already exists
            var existingUserByPhone = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhoneNumber || u.UserName == normalizedPhoneNumber);
            if (existingUserByPhone != null)
            {
                TempData["Error"] = "کاربری با این شماره تلفن قبلاً ثبت نام کرده است.";
                return RedirectToAction("ManageAdmins");
            }

            var user = new ApplicationUser
            {
                UserName = normalizedPhoneNumber, // Use normalized phone number as username
                PhoneNumber = normalizedPhoneNumber, // Set normalized phone number
                Email = email,
                FullName = fullName, // Set the full name
                EmailConfirmed = true,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Add to the appropriate Identity role based on the provided UserRole
                await _userManager.AddToRoleAsync(user, role.ToString());

                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserCreated,
                        $"کاربر جدید ایجاد شد: {fullName} ({normalizedPhoneNumber}) با نقش {role}",
                        JsonSerializer.Serialize(new { UserId = user.Id, Role = role, OriginalInput = userName, NormalizedPhone = normalizedPhoneNumber }),
                        "ApplicationUser",
                        null,
                        null,
                        user.Id
                    );
                }

                TempData["Success"] = $"کاربر {fullName} با شماره تلفن {PhoneNumberService.GetDisplayFormat(normalizedPhoneNumber)} با موفقیت ایجاد شد.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Change Admin User Role
        /// تغییر نقش کاربر ادمین
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Users_ChangeRole)]
        public async Task<IActionResult> ChangeAdminRole(string userId, UserRole newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "کاربر یافت نشد." });
                }

                TempData["Error"] = "کاربر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent users from changing their own role
            var currentUserCheck = await _userManager.GetUserAsync(User);
            if (currentUserCheck != null && currentUserCheck.Id == userId)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "نمی‌توانید نقش خود را تغییر دهید." });
                }

                TempData["Error"] = "نمی‌توانید نقش خود را تغییر دهید.";
                return RedirectToAction("ManageAdmins");
            }

            var oldRole = user.Role;

            // Update the user's role in the database
            user.Role = newRole;
            var updateResult = await _userManager.UpdateAsync(user);

            if (updateResult.Succeeded)
            {
                // Update ASP.NET Identity roles
                var oldRoleName = oldRole.ToString();
                var newRoleName = newRole.ToString();

                if (await _userManager.IsInRoleAsync(user, oldRoleName))
                {
                    await _userManager.RemoveFromRoleAsync(user, oldRoleName);
                }
                if (!await _userManager.IsInRoleAsync(user, newRoleName))
                {
                    await _userManager.AddToRoleAsync(user, newRoleName);
                }
                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserUpdated,
                        $"نقش کاربر {user.UserName} تغییر یافت از {oldRole} به {newRole}",
                        JsonSerializer.Serialize(new { UserId = user.Id, OldRole = oldRole, NewRole = newRole }),
                        "ApplicationUser",
                        null,
                        oldRole.ToString(),
                        newRole.ToString()
                    );
                }

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"نقش کاربر {user.UserName} با موفقیت تغییر یافت." });
                }

                TempData["Success"] = $"نقش کاربر {user.UserName} با موفقیت تغییر یافت.";
            }
            else
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = string.Join(", ", updateResult.Errors.Select(e => e.Description)) });
                }

                TempData["Error"] = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Delete Admin User
        /// حذف کاربر ادمین
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Users_Delete)]
        public async Task<IActionResult> DeleteAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "کاربر یافت نشد." });
                }

                TempData["Error"] = "کاربر یافت نشد.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent users from deleting themselves
            var currentUserCheck = await _userManager.GetUserAsync(User);
            if (currentUserCheck != null && currentUserCheck.Id == userId)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "نمی‌توانید خود را حذف کنید." });
                }

                TempData["Error"] = "نمی‌توانید خود را حذف کنید.";
                return RedirectToAction("ManageAdmins");
            }

            // Prevent deleting the last Admin
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            if (adminRole != null)
            {
                var adminCount = _context.UserRoles.Count(ur => ur.RoleId == adminRole.Id);
                var isUserAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (isUserAdmin && adminCount <= 1)
                {
                    // Check if this is an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "نمی‌توان آخرین ادمین را حذف کرد." });
                    }

                    TempData["Error"] = "نمی‌توان آخرین ادمین را حذف کرد.";
                    return RedirectToAction("ManageAdmins");
                }
            }

            var userName = user.UserName;
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                // Log activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.UserDeleted,
                        $"کاربر ادمین حذف شد: {userName}",
                        JsonSerializer.Serialize(new { DeletedUserId = userId, DeletedUserName = userName }),
                        "ApplicationUser",
                        null
                    );
                }

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"کاربر ادمین {userName} با موفقیت حذف شد." });
                }

                TempData["Success"] = $"کاربر ادمین {userName} با موفقیت حذف شد.";
            }
            else
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
                }

                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("ManageAdmins");
        }

        /// <summary>
        /// Export Admin Activities
        /// دریافت فعالیت‌های ادمین
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportActivities(
            string? adminUserId = null,
            AdminActivityType? activityType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var activities = await _adminActivityService.GetAllActivitiesAsync(
                adminUserId, activityType, fromDate, toDate);

            // Log export activity
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                await _adminActivityService.LogDataExportAsync(
                    currentUser.Id,
                    currentUser.UserName ?? "Unknown",
                    "AdminActivities",
                    activities.Count
                );
            }

            // Create CSV content
            var csv = "Id,AdminUserId,AdminUsername,ActivityType,Description,Timestamp,IpAddress,IsSuccess\n";
            foreach (var activity in activities)
            {
                csv += $"{activity.Id},{activity.AdminUserId},{activity.AdminUsername},{activity.ActivityType},{activity.Description},{activity.Timestamp},{activity.IpAddress},{activity.IsSuccess}\n";
            }

            var fileName = $"admin_activities_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        /// <summary>
        /// Edit Admin User
        /// ویرایش کاربر ادمین
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Users_Edit)]
        public async Task<IActionResult> EditAdmin(string userId, string email, string fullName)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "کاربر یافت نشد" });
                }

                // Allow editing self - no restriction for basic info updates
                var currentUser = await _userManager.GetUserAsync(User);

                var oldEmail = user.Email;
                var oldFullName = user.FullName;

                // Update user information
                user.Email = email;
                user.FullName = fullName;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Log the activity
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogUserEditAsync(
                            currentUser.Id,
                            currentUser.UserName ?? "Unknown",
                            user.Id,
                            user.UserName ?? "Unknown",
                            $"Email: {oldEmail} → {email}, FullName: {oldFullName} → {fullName}"
                        );
                    }

                    return Json(new { success = true, message = "اطلاعات کاربر با موفقیت بروزرسانی شد" });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Json(new { success = false, message = $"خطا در بروزرسانی: {errors}" });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطا در بروزرسانی اطلاعات کاربر" });
            }
        }

        /// <summary>
        /// Change Admin Password
        /// تغییر رمز عبور ادمین
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ChangeAdminPassword(string userId, string newPassword, string confirmPassword)
        {
            try
            {
                // Validate passwords match
                if (newPassword != confirmPassword)
                {
                    return Json(new { success = false, message = "رمز عبور و تکرار آن باید یکسان باشند" });
                }

                // Validate password length
                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                {
                    return Json(new { success = false, message = "رمز عبور باید حداقل ۶ کاراکتر باشد" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "کاربر یافت نشد" });
                }

                // Allow changing own password - remove restriction
                var currentUser = await _userManager.GetUserAsync(User);

                // Remove current password and set new one
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    // Log the activity
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogActivityAsync(
                            currentUser.Id,
                            currentUser.UserName ?? "Unknown",
                            AdminActivityType.UserUpdated,
                            $"Password changed for user: {user.UserName ?? user.Email}",
                            $"Admin {currentUser.UserName} changed password for user {user.UserName ?? user.Email}",
                            "User",
                            null,
                            "[PROTECTED]",
                            "[PASSWORD_CHANGED]",
                            true
                        );
                    }

                    return Json(new { success = true, message = "رمز عبور با موفقیت تغییر یافت" });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Json(new { success = false, message = $"خطا در تغییر رمز عبور: {errors}" });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطا در تغییر رمز عبور" });
            }
        }
    }
}
