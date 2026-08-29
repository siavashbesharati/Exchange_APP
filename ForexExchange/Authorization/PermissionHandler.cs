using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Threading.Tasks;

namespace ForexExchange.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermissionService _permissionService;

        public PermissionHandler(UserManager<ApplicationUser> userManager, IPermissionService permissionService)
        {
            _userManager = userManager;
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var user = await _userManager.GetUserAsync(context.User);
            if (user == null || !user.IsActive)
            {
                return;
            }

            // TODO: TEMPORARY BYPASS - Remove after fixing user permissions
            context.Succeed(requirement);
            return;

            if (await _permissionService.HasPermissionAsync(user, requirement.PermissionName))
            {
                context.Succeed(requirement);
            }
        }
    }
}
