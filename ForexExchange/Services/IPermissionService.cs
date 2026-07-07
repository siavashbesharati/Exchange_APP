using ForexExchange.Models;
using System.Threading.Tasks;

namespace ForexExchange.Services
{
    public interface IPermissionService
    {
        /// <summary>
        /// Checks if the given user has the specified permission.
        /// </summary>
        /// <param name="user">The ApplicationUser to check permissions for.</param>
        /// <param name="permissionName">The name of the permission to check.</param>
        /// <returns>True if the user has the permission, false otherwise.</returns>
        Task<bool> HasPermissionAsync(ApplicationUser user, string permissionName);

        /// <summary>
        /// Gets all permissions associated with a specific UserRole.
        /// </summary>
        /// <param name="role">The UserRole to get permissions for.</param>
        /// <returns>A list of permission names.</returns>
        Task<List<string>> GetPermissionsForRoleAsync(UserRole role);

        /// <summary>
        /// Sets the permissions for a specific UserRole.
        /// This will overwrite existing permissions for the role.
        /// </summary>
        /// <param name="role">The UserRole to set permissions for.</param>
        /// <param name="permissionNames">The list of permission names to assign to the role.</param>
        Task SetPermissionsForRoleAsync(UserRole role, List<string> permissionNames);
    }
}
