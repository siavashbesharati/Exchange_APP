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
        /// Format: "SEND {amount} {currency} at rate {rate} to {customerName}, RECEIVE {amount} {currency} | ID {orderId}"
        /// For FromCurrency: SEND (customer sends FromCurrency)
        /// For ToCurrency: RECEIVE (customer receives ToCurrency)
        /// </summary>
        public static string GenerateOrderDescription(Order order, string currencyCode, bool isFromCurrency)
        {
            var fromCurrencyCode = order.FromCurrency?.Code ?? "";
            var toCurrencyCode = order.ToCurrency?.Code ?? "";
            var customerName = order.Customer?.FullName ?? "Unknown";

            if (isFromCurrency)
            {
                // Customer sends FromCurrency (negative transaction)
                return $"SEND {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} at rate {order.Rate:N4} to {customerName}, RECEIVE {order.ToAmount.FormatCurrency(toCurrencyCode)} {toCurrencyCode} | ID {order.Id}";
            }
            else
            {
                // Customer receives ToCurrency (positive transaction)
                return $"RECEIVE {order.ToAmount.FormatCurrency(toCurrencyCode)} {toCurrencyCode} from {customerName}, SEND {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} at rate {order.Rate:N4} | ID {order.Id}";
            }
        }

        /// <summary>
        /// Generates note for Order transaction (without customer info)
        /// Format: "SEND {amount} {currency} at rate {rate}, RECEIVE {amount} {currency} | ID {orderId}"
        /// For FromCurrency: SEND
        /// For ToCurrency: RECEIVE
        /// </summary>
        public static string GenerateOrderNote(Order order, string currencyCode, bool isFromCurrency)
        {
            var fromCurrencyCode = order.FromCurrency?.Code ?? "";
            var toCurrencyCode = order.ToCurrency?.Code ?? "";

            if (isFromCurrency)
            {
                return $"SEND {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} at rate {order.Rate:N4}, RECEIVE {order.ToAmount.FormatCurrency(toCurrencyCode)} {toCurrencyCode} | ID {order.Id}";
            }
            else
            {
                return $"RECEIVE {order.ToAmount.FormatCurrency(toCurrencyCode)} {toCurrencyCode}, SEND {order.FromAmount.FormatCurrency(fromCurrencyCode)} {fromCurrencyCode} at rate {order.Rate:N4} | ID {order.Id}";
            }
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
        /// Format: "BUY {amount} {currency} - Order #{orderId} - Rate: {rate}" or "SELL {amount} {currency} - Order #{orderId} - Rate: {rate}"
        /// </summary>
        public static string GeneratePoolHistoryDescription(string currencyCode, decimal transactionAmount, string poolTransactionType, int? orderId = null, decimal? rate = null)
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

            if (orderId.HasValue && rate.HasValue)
            {
                description += $" - Order #{orderId.Value} - Rate: {rate.Value:N4}";
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

