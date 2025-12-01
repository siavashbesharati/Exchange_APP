using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    /// <summary>
    /// DTO for previewing accounting document effects on customer and bank account balances.
    /// Shows the before/after state for all affected parties.
    /// </summary>
    public class AccountingDocumentPreviewEffectsDto
    {
        public int DocumentId { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Effects on customer balances (if customers are involved)
        /// </summary>
        public List<CustomerBalanceEffect> CustomerEffects { get; set; } = new List<CustomerBalanceEffect>();
        
        /// <summary>
        /// Effects on bank account balances (if system bank accounts are involved)
        /// </summary>
        public List<BankAccountBalanceEffect> BankAccountEffects { get; set; } = new List<BankAccountBalanceEffect>();
        
        /// <summary>
        /// Warnings about potential issues (negative balances, currency mismatches, etc.)
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents the effect of an accounting document on a specific customer's balance
    /// </summary>
    public class CustomerBalanceEffect
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal NewBalance { get; set; }
        public string Role { get; set; } = string.Empty; // "Payer" or "Receiver"
    }

    /// <summary>
    /// Represents the effect of an accounting document on a specific bank account's balance
    /// </summary>
    public class BankAccountBalanceEffect
    {
        public int BankAccountId { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        // Display property (from Currency navigation)
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal NewBalance { get; set; }
        public string Role { get; set; } = string.Empty; // "Payer" or "Receiver"
    }
}