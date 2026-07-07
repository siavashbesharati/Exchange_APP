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
            if (context.User == null)
            {
                return;
            }

            var user = await _userManager.GetUserAsync(context.User);
            if (user == null)
            {
                return;
            }

            if (await _permissionService.HasPermissionAsync(user, requirement.PermissionName))
            {
                context.Succeed(requirement);
            }
        }
    }
}
