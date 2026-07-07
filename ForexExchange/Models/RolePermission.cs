using System.ComponentModel.DataAnnotations;
using ForexExchange.Models;

namespace ForexExchange.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)] // Max length for IdentityRole.Name
        public string RoleName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string PermissionName { get; set; } = string.Empty;
    }
}
