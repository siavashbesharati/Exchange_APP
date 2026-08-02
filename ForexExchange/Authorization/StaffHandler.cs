using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;

namespace ForexExchange.Authorization
{
    /// <summary>
    /// Staff gate based on Identity roles (AspNetUserRoles).
    /// Falls back to ApplicationUser.Role enum for legacy users missing AspNetUserRoles rows.
    /// </summary>
    public class StaffHandler : AuthorizationHandler<StaffRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public StaffHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            StaffRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
                return;

            var user = await _userManager.GetUserAsync(context.User);
            if (user == null || !user.IsActive)
                return;

            var roles = await _userManager.GetRolesAsync(user);
            var isStaff = roles.Any(r =>
                !string.Equals(r, "Customer", StringComparison.OrdinalIgnoreCase));

            // Legacy bridge: enum Role set to staff but AspNetUserRoles empty/outdated
            if (!isStaff && user.Role != UserRole.Customer)
                isStaff = true;

            if (isStaff)
                context.Succeed(requirement);
        }
    }
}
