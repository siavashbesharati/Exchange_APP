using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class Customer
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(20)]
        public string SecondaryPhoneNumber { get; set; } = string.Empty;

        [Required]
        public bool Gender { get; set; } = true;

        [StringLength(200)]
        public string Email { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string NationalId { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
        
        public bool IsSystem { get; set; } = false; // Indicates if this is a system customer (for exchange operations)
        
        public bool IsShareHolder { get; set; } = false; // Indicates if this customer is a shareholder (سهامدار)
        
        // Navigation properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        
        // New navigation properties for the updated architecture
        public ICollection<CustomerBalance> Balances { get; set; } = new List<CustomerBalance>();
    }
}
