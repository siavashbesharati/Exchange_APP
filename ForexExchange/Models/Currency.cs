using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class Currency
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(4)]
        public string Code { get; set; } = string.Empty; // USD,USDT, EUR, AED, etc.
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty; // it uses for flag icon.
        
        [Required]
        [StringLength(50)]
        public string PersianName { get; set; } = string.Empty; // دلار آمریکا, یورو, etc.
        
        [StringLength(5)]
        public string Symbol { get; set; } = string.Empty; // $, €, etc.
        
        public bool IsActive { get; set; } = true;
        
        public int DisplayOrder { get; set; }
        
        public int RatePriority { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Navigation properties
        public virtual ICollection<ExchangeRate> FromCurrencyRates { get; set; } = new List<ExchangeRate>();
        public virtual ICollection<ExchangeRate> ToCurrencyRates { get; set; } = new List<ExchangeRate>();
        public virtual ICollection<ExchangeRate> LegacyRates { get; set; } = new List<ExchangeRate>();
        public virtual ICollection<Order> FromCurrencyOrders { get; set; } = new List<Order>();
        public virtual ICollection<Order> ToCurrencyOrders { get; set; } = new List<Order>();
        public virtual ICollection<AccountingDocument> CurrencyDocuments { get; set; } = new List<AccountingDocument>();
        public virtual ICollection<CustomerBalance> CustomerBalances { get; set; } = new List<CustomerBalance>();
        public virtual ICollection<CustomerBalanceHistory> CustomerBalanceHistories { get; set; } = new List<CustomerBalanceHistory>();
        public virtual ICollection<CurrencyPoolHistory> CurrencyPoolHistories { get; set; } = new List<CurrencyPoolHistory>();
        public virtual ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        public virtual ICollection<BankAccountBalance> BankAccountBalances { get; set; } = new List<BankAccountBalance>();
        public virtual ICollection<CurrencyPool> CurrencyPools { get; set; } = new List<CurrencyPool>();
    }
}
