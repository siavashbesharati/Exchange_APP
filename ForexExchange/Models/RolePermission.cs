using System.ComponentModel.DataAnnotations;
using ForexExchange.Models;

namespace ForexExchange.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        [Required]
        public UserRole UserRole { get; set; }

        [Required]
        [StringLength(100)]
        public string PermissionName { get; set; } = string.Empty;
    }
}
