namespace ForexExchange.Models
{
    // DTO for previewing order effects
    public class OrderPreviewEffectsDto
    {
        public int CustomerId { get; set; }
        public int FromCurrencyId { get; set; }
        public int ToCurrencyId { get; set; }
        // Display properties (from Currency navigation)
        public string FromCurrencyCode { get; set; } = string.Empty;
        public string ToCurrencyCode { get; set; } = string.Empty;
        public decimal OrderFromAmount { get; set; }
        public decimal OrderToAmount { get; set; }
        public decimal OldCustomerBalanceFrom { get; set; }
        public decimal OldCustomerBalanceTo { get; set; }
        public decimal NewCustomerBalanceFrom { get; set; }
        public decimal NewCustomerBalanceTo { get; set; }
        public decimal OldPoolBalanceFrom { get; set; }
        public decimal OldPoolBalanceTo { get; set; }
        public decimal NewPoolBalanceFrom { get; set; }
        public decimal NewPoolBalanceTo { get; set; }
    }
}