using System.Collections.Generic;
using ForexExchange.Models;
using Microsoft.AspNetCore.Identity; // Needed for IdentityRole

namespace ForexExchange.Models.ViewModels
{
    public class RolePermissionViewModel
    {
        public List<IdentityRole> Roles { get; set; } = new List<IdentityRole>(); // Changed back to IdentityRole
        public List<string> AllPermissions { get; set; } = new List<string>();
        public UserRole SelectedRole { get; set; }
        public List<string> CurrentRolePermissions { get; set; } = new List<string>();
    }
}
