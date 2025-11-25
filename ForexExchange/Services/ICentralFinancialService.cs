using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Centralized financial service interface that combines customer balance management,
    /// currency pool operations, and bank account balance tracking with complete audit trail.
    /// This service ensures data consistency and provides event sourcing capabilities.
    /// </summary>
    public interface ICentralFinancialService
    {


        /// <summary>
        /// Simulates the effects of an order on customer and pool balances (no DB changes)
        /// </summary>
        Task<OrderPreviewEffectsDto> PreviewOrderEffectsAsync(Order order);

        /// <summary>
        /// Simulates the effects of an account document on customer and bank balances (no DB changes)
        /// </summary>
        Task<AccountingDocumentPreviewEffectsDto> PreviewAccountingDocumentEffectsAsync(AccountingDocument accountingDocument);




        /// <summary>
        /// Processes order creation - creates dual-currency impact (payment + receipt transactions)
        /// Preserves exact logic from existing CustomerFinancialHistoryService
        /// </summary>
        Task ProcessOrderCreationAsync(Order order, string performedBy = "System");

        /// <summary>
        /// Processes accounting document verification - updates customer balances
        /// Preserves exact logic from existing document processing
        /// </summary>
        Task ProcessAccountingDocumentAsync(AccountingDocument document, string performedBy = "System");


        /// <summary>
        /// Safely deletes an order by reversing its financial impacts
        /// </summary>
        Task DeleteOrderAsync(Order order, string performedBy = "Admin");

        /// <summary>
        /// Safely deletes an accounting document by reversing its financial impacts
        /// </summary>
        Task DeleteAccountingDocumentAsync(AccountingDocument document, string performedBy = "Admin");










        /// <summary>
        /// Creates a manual customer balance history record with specified transaction date.
        /// This is useful for manual adjustments, corrections, or importing historical data.
        /// After creating manual records, use RecalculateAllBalancesFromTransactionDatesAsync to ensure coherence.
        /// Automatically sends notifications to admin users (excluding the performing user).
        /// </summary>
        Task CreateManualCustomerBalanceHistoryAsync(int customerId, string currencyCode, decimal amount,
            string reason, DateTime transactionDate, string performedBy = "Manual Entry", string? transactionNumber = null, string? performingUserId = null);

        /// <summary>
        /// Deletes a manual customer balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.Manual) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// Automatically sends notifications to admin users (excluding the performing user).
        /// </summary>
        Task DeleteManualCustomerBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null);

        /// <summary>
        /// Creates a manual currency pool balance history record with specified transaction date.
        /// This is useful for manual adjustments, corrections, or importing historical data.
        /// After creating manual records, use RecalculateAllBalancesFromTransactionDatesAsync to ensure coherence.
        /// Automatically sends notifications to admin users (excluding the performing user).
        /// </summary>
        Task CreateManualPoolBalanceHistoryAsync(string currencyCode, decimal adjustmentAmount,
            string reason, DateTime transactionDate, string performedBy = "Manual Entry", string? performingUserId = null);

        /// <summary>
        /// Deletes a manual currency pool balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.ManualEdit) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// Automatically sends notifications to admin users (excluding the performing user).
        /// </summary>
        Task DeleteManualPoolBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null);

        /// <summary>
        /// Creates a manual bank account balance history record with specified transaction date.
        /// This is useful for manual adjustments, corrections, or importing historical data.
        /// After creating manual records, use RecalculateAllBalancesFromTransactionDatesAsync to ensure coherence.
        /// Automatically sends notifications to admin users (excluding the performing user).
        /// </summary>
        Task CreateManualBankAccountBalanceHistoryAsync(int bankAccountId, decimal amount,
            string reason, DateTime transactionDate, string performedBy = "Manual Entry", string? performingUserId = null);

        /// <summary>
        /// Deletes a manual bank account balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.ManualEdit) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// Automatically sends notifications to admin users (excluding the performing user).
        /// </summary>
        Task DeleteManualBankAccountBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null);

        /// <summary>
        /// Comprehensive rebuild of all financial balances based on new IsFrozen strategy:
        /// - Pool balances rebuilt from non-deleted AND non-frozen orders only with coherent history starting from zero
        /// - Bank account balances rebuilt from non-deleted AND non-frozen documents only with coherent history starting from zero
        /// - Customer balance history rebuilt from non-deleted orders, documents, and manual records (including frozen orders/documents)
        /// - Active buy/sell counts recalculated properly based on non-frozen orders
        /// 
        /// This ensures frozen historical records don't affect current balance calculations
        /// but are preserved for customer balance history audit trail, including manual adjustments.
        /// Creates coherent balance history chains starting from zero before first non-frozen record.
        /// </summary>
    Task RebuildAllFinancialBalancesAsync(string performedBy = "System");

    /// <summary>
    /// Sets IsFrozen=true for all orders and accounting documents to exclude them from future balance calculations.
    /// Returns the number of entities that were updated during the operation.
    /// </summary>
    Task<int> FreezeAllOrdersAndDocumentsAsync(string performedBy = "System");








    }
}


/// <summary>
/// DTO for customer financial history to preserve existing API contract
/// </summary>
public class CustomerFinancialHistoryDto
{
    public required Customer Customer { get; set; }
    public required List<CustomerBalance> Balances { get; set; }
    public required List<FinancialTransactionDto> Transactions { get; set; }
    public required Dictionary<string, decimal> InitialBalances { get; set; }
}

/// <summary>
/// DTO for financial transactions to preserve existing display format
/// </summary>
public class FinancialTransactionDto
{
    public DateTime Date { get; set; }
    public required string Type { get; set; } // "Order" or "Document"
    public required string Description { get; set; }
    public required string CurrencyCode { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningBalance { get; set; }
    public int? OrderId { get; set; }
    public int? DocumentId { get; set; }
    public string? Notes { get; set; }
}
