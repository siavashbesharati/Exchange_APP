using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ForexDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionService(ForexDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> HasPermissionAsync(ApplicationUser user, string permissionName)
        {
            if (user == null || string.IsNullOrWhiteSpace(permissionName))
                return false;

            var normalizedPermission = NormalizePermissionName(permissionName);

            // Identity roles only (supports seeded + custom panel roles)
            var identityRoles = await _userManager.GetRolesAsync(user);
            var roleNames = identityRoles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Programmer Identity role always has all permissions
            if (roleNames.Any(r => r.Equals("Programmer", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (roleNames.Count == 0)
                return false;

            var rolePermissions = await _context.RolePermissions
                .Where(rp => roleNames.Contains(rp.RoleName))
                .Select(rp => rp.PermissionName)
                .ToListAsync();

            return rolePermissions.Any(p => NormalizePermissionName(p) == normalizedPermission);
        }

        public async Task<List<string>> GetPermissionsForRoleAsync(string roleName)
        {
            if (roleName == "Programmer")
                return GetAllDefinedPermissions();

            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleName == roleName)
                .Select(rp => rp.PermissionName)
                .ToListAsync();

            return permissions
                .Select(NormalizePermissionName)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public async Task SetPermissionsForRoleAsync(string roleName, List<string> permissionNames)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.", nameof(roleName));

            if (roleName == "Programmer")
                return;

            var normalizedIncoming = (permissionNames ?? new List<string>())
                .Select(NormalizePermissionName)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var existingPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleName == roleName)
                .ToListAsync();
            _context.RolePermissions.RemoveRange(existingPermissions);

            var newRolePermissions = normalizedIncoming.Select(pn => new RolePermission
            {
                RoleName = roleName,
                PermissionName = pn
            }).ToList();

            _context.RolePermissions.AddRange(newRolePermissions);
            await _context.SaveChangesAsync();
        }

        public async Task NormalizeStoredPermissionNamesAsync()
        {
            var rows = await _context.RolePermissions.ToListAsync();
            if (rows.Count == 0)
                return;

            var knownPermissions = GetAllDefinedPermissions()
                .ToDictionary(NormalizePermissionName, p => p, StringComparer.Ordinal);

            var changed = false;
            foreach (var row in rows)
            {
                var trimmed = NormalizePermissionName(row.PermissionName);
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (knownPermissions.TryGetValue(trimmed, out var canonical))
                    trimmed = canonical;

                if (!string.Equals(row.PermissionName, trimmed, StringComparison.Ordinal))
                {
                    row.PermissionName = trimmed;
                    changed = true;
                }
            }

            var duplicates = rows
                .GroupBy(r => (r.RoleName, NormalizePermissionName(r.PermissionName)))
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Skip(1))
                .ToList();

            if (duplicates.Count > 0)
            {
                _context.RolePermissions.RemoveRange(duplicates);
                changed = true;
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        public static string NormalizePermissionName(string? permissionName)
        {
            return (permissionName ?? string.Empty).Trim();
        }

        public static List<string> GetAllDefinedPermissions()
        {
            return typeof(Permissions)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                .Select(fi => NormalizePermissionName(fi.GetRawConstantValue()?.ToString()))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
