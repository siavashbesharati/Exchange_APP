using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ForexExchange.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ForexDbContext _context;

        public PermissionService(ForexDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermissionAsync(ApplicationUser user, string permissionName)
        {
            if (user == null || string.IsNullOrEmpty(permissionName))
            {
                return false;
            }

            // Programmers always have all permissions
            if (user.Role == UserRole.Programmer)
            {
                return true;
            }

            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleName == user.Role.ToString() && rp.PermissionName == permissionName)
                .AnyAsync();

            return rolePermissions;
        }

        public async Task<List<string>> GetPermissionsForRoleAsync(string roleName)
        {
            // Programmers implicitly have all permissions, but we might want to list them for UI purposes
            if (roleName == UserRole.Programmer.ToString())
            {
                // Return all defined permissions from the static class
                var allPermissions = typeof(Permissions)
                    .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
                    .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                    .Select(fi => fi.GetRawConstantValue()?.ToString() ?? string.Empty)
                    .ToList();
                return allPermissions;
            }

            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleName == roleName)
                .Select(rp => rp.PermissionName)
                .ToListAsync();

            return permissions;
        }

        public async Task SetPermissionsForRoleAsync(string roleName, List<string> permissionNames)
        {
            // Remove existing permissions for the role
            var existingPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleName == roleName)
                .ToListAsync();
            _context.RolePermissions.RemoveRange(existingPermissions);

            // Add new permissions
            var newRolePermissions = permissionNames.Select(pn => new RolePermission
            {
                RoleName = roleName,
                PermissionName = pn
            }).ToList();

            _context.RolePermissions.AddRange(newRolePermissions);
            await _context.SaveChangesAsync();
        }
    }
}
