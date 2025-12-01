using ForexExchange.Models;
using ForexExchange.Extensions;

namespace ForexExchange.Helpers
{
    /// <summary>
    /// Helper class for generating consistent English-formatted descriptions and notes for history records
    /// </summary>
    public static class HistoryDescriptionHelper
    {
        /// <summary>
        /// Generates description for Order transaction in CustomerBalanceHistory
        /// Format: "BUY {amount from} {from currency} | SELL {to currency amount} {to currency code} | {rate} | {customerName} | {description}"
        /// Note: description parameter should be the original Notes value, not the generated description to avoid duplication
        /// </summary>
        public static string GenerateOrderDescription(Order order, string currencyCode, bool isFromCurrency, string? originalDescription = null)
        {
            var fromCurrencyCode = order.FromCurrency?.Code ?? "";
            var toCurrencyCode = order.ToCurrency?.Code ?? "";
            var customerName = order.Customer?.FullName ?? "Unknown";
            
            // Use originalDescription if provided, otherwise use order.Notes but only if it doesn't already contain the generated format
            string description = "";
            if (!string.IsNullOrWhiteSpace(originalDescription))
            {
                description = originalDescription;
            }
            else if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                // Check if Notes already contains the generated format (to avoid duplication)
                var generatedPattern = $"BUY {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} | SELL";
                if (!order.Notes.Contains(generatedPattern))
                {
                    description = order.Notes;
                }
            }

            // Format: BUY {amount from} {from currency} | SELL {to currency amount} {to currency code} | {rate} | {customerName} | {description}
            var result = $"BUY {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} | SELL {order.ToAmount.FormatCurrency(toCurrencyCode)} {toCurrencyCode} | {order.Rate:N4} | {customerName}";
            
            if (!string.IsNullOrWhiteSpace(description))
            {
                result += $" | {description}";
            }
            
            return result;
        }

        /// <summary>
        /// Generates note for Order transaction
        /// Format: "BUY {amount from} {from currency} | SELL {to currency amount} {to currency code} | {rate} | {customerName} | {description}"
        /// Note: description parameter should be the original Notes value, not the generated description to avoid duplication
        /// </summary>
        public static string GenerateOrderNote(Order order, string currencyCode, bool isFromCurrency, string? originalDescription = null)
        {
            var fromCurrencyCode = order.FromCurrency?.Code ?? "";
            var toCurrencyCode = order.ToCurrency?.Code ?? "";
            var customerName = order.Customer?.FullName ?? "Unknown";
            
            // Use originalDescription if provided, otherwise use order.Notes but only if it doesn't already contain the generated format
            string description = "";
            if (!string.IsNullOrWhiteSpace(originalDescription))
            {
                description = originalDescription;
            }
            else if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                // Check if Notes already contains the generated format (to avoid duplication)
                var generatedPattern = $"BUY {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} | SELL";
                if (!order.Notes.Contains(generatedPattern))
                {
                    description = order.Notes;
                }
            }

            // Format: BUY {amount from} {from currency} | SELL {to currency amount} {to currency code} | {rate} | {customerName} | {description}
            var result = $"BUY {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} | SELL {order.ToAmount.FormatCurrency(toCurrencyCode)} {toCurrencyCode} | {order.Rate:N4} | {customerName}";
            
            if (!string.IsNullOrWhiteSpace(description))
            {
                result += $" | {description}";
            }
            
            return result;
        }

        /// <summary>
        /// Generates description for AccountingDocument transaction in CustomerBalanceHistory
        /// Format: "{from} Transfer {amount} {currencyCode} To {to} via {documentType} transaction_id : {trackingCode}, {description}"
        /// </summary>
        public static string GenerateDocumentDescription(AccountingDocument document, string role)
        {
            var amount = document.Amount;
            var currencyCode = document.Currency?.Code ?? document.CurrencyCode;
            var documentType = document.Type == DocumentType.Cash ? "cash" : "havala";
            var trackingCode = document.ReferenceNumber ?? "";
            var description = document.Description ?? "";

            if (role == "پرداخت کننده" || role == "Payer")
            {
                // Payer perspective: "From {payerName} Transfer {amount} {currency} To {receiverName} via {type}"
                var fromName = document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue
                    ? document.PayerCustomer?.FullName ?? "Unknown Customer"
                    : document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue
                        ? $"{document.PayerBankAccount?.BankName ?? "Bank"} ({document.PayerBankAccount?.AccountNumber ?? ""})"
                        : "System";

                var toName = document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue
                    ? document.ReceiverCustomer?.FullName ?? "Unknown Customer"
                    : document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue
                        ? $"{document.ReceiverBankAccount?.BankName ?? "Bank"} ({document.ReceiverBankAccount?.AccountNumber ?? ""})"
                        : "System";

                var result = $"{fromName} Transfer {amount.FormatCurrency(currencyCode)} {currencyCode} To {toName} via {documentType}";
                if (!string.IsNullOrWhiteSpace(trackingCode))
                {
                    result += $" transaction_id : {trackingCode}";
                }
                if (!string.IsNullOrWhiteSpace(description))
                {
                    result += $", {description}";
                }
                return result;
            }
            else // Receiver
            {
                // Receiver perspective: "From {payerName} Transfer {amount} {currency} To {receiverName} via {type}"
                var fromName = document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue
                    ? document.PayerCustomer?.FullName ?? "Unknown Customer"
                    : document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue
                        ? $"{document.PayerBankAccount?.BankName ?? "Bank"} ({document.PayerBankAccount?.AccountNumber ?? ""})"
                        : "System";

                var toName = document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue
                    ? document.ReceiverCustomer?.FullName ?? "Unknown Customer"
                    : document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue
                        ? $"{document.ReceiverBankAccount?.BankName ?? "Bank"} ({document.ReceiverBankAccount?.AccountNumber ?? ""})"
                        : "System";

                var result = $"{fromName} Transfer {amount.FormatCurrency(currencyCode)} {currencyCode} To {toName} via {documentType}";
                if (!string.IsNullOrWhiteSpace(trackingCode))
                {
                    result += $" transaction_id : {trackingCode}";
                }
                if (!string.IsNullOrWhiteSpace(description))
                {
                    result += $", {description}";
                }
                return result;
            }
        }

        /// <summary>
        /// Generates note for AccountingDocument transaction
        /// Format: "{DocumentType} - Amount: {amount} {currency} transaction_id : {trackingCode}, {description}"
        /// </summary>
        public static string GenerateDocumentNote(AccountingDocument document)
        {
            // Use Type.ToString() to get English enum name instead of GetDisplayName() which returns Persian
            var documentType = document.Type == DocumentType.Cash ? "cash" : "havala";
            var amount = document.Amount;
            var currencyCode = document.Currency?.Code ?? document.CurrencyCode;
            var trackingCode = document.ReferenceNumber ?? "";
            var description = document.Description ?? "";
            
            var note = $"{documentType} - Amount: {amount.FormatCurrency(currencyCode)} {currencyCode}";
            
            if (!string.IsNullOrWhiteSpace(trackingCode))
            {
                note += $" transaction_id : {trackingCode}";
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                note += $", {description}";
            }

            return note;
        }

        /// <summary>
        /// Generates description for CurrencyPoolHistory
        /// Format: "BUY {amount} {currency} | Rate: {rate} | {customerName}" or "SELL {amount} {currency} | Rate: {rate} | {customerName}"
        /// </summary>
        public static string GeneratePoolHistoryDescription(string currencyCode, decimal transactionAmount, string poolTransactionType, int? orderId = null, decimal? rate = null, string? customerName = null, string? orderDescription = null)
        {
            var amount = Math.Abs(transactionAmount);
            var description = "";

            if (poolTransactionType == "Buy")
            {
                description = $"BUY {amount.FormatCurrency(currencyCode)} {currencyCode}";
            }
            else if (poolTransactionType == "Sell")
            {
                description = $"SELL {amount.FormatCurrency(currencyCode)} {currencyCode}";
            }
            else
            {
                description = $"TRANSACTION {transactionAmount.FormatCurrency(currencyCode)} {currencyCode}";
            }

            if (rate.HasValue)
            {
                description += $" | Rate: {rate.Value:N4}";
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                description += $" | {customerName}";
            }

            if (!string.IsNullOrWhiteSpace(orderDescription))
            {
                description += $" | {orderDescription}";
            }

            return description;
        }

        /// <summary>
        /// Generates description for BankAccountBalanceHistory
        /// Format: "{Title} - Amount: {amount} {currency} - Account: {BankName} ({AccountNumber})"
        /// </summary>
        public static string GenerateBankHistoryDescription(AccountingDocument document, BankAccount? bankAccount)
        {
            var title = document.Title;
            var amount = document.Amount;
            var currencyCode = document.CurrencyCode;
            var bankName = bankAccount?.BankName ?? "Unknown Bank";
            var accountNumber = bankAccount?.AccountNumber ?? "";

            return $"{title} - Amount: {amount.FormatCurrency(currencyCode)} {currencyCode} - Account: {bankName} ({accountNumber})";
        }

        /// <summary>
        /// Generates description for ManualEdit transactions
        /// </summary>
        public static string GenerateManualDescription(string reason, decimal amount, string currencyCode)
        {
            return $"MANUAL ADJUSTMENT - {reason} | Amount: {amount.FormatCurrency(currencyCode)} {currencyCode}";
        }
    }
}

