using System.Collections.Generic;
using ForexExchange.Models;
//using Microsoft.AspNetCore.Identity; // Not needed if using UserRole enum directly

namespace ForexExchange.Models.ViewModels
{
    public class RolePermissionViewModel
    {
        public List<UserRole> Roles { get; set; } = new List<UserRole>(); // Changed from IdentityRole
        public List<string> AllPermissions { get; set; } = new List<string>();
        public UserRole SelectedRole { get; set; }
        public List<string> CurrentRolePermissions { get; set; } = new List<string>();
    }
}
