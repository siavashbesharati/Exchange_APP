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
        /// Format: "RECEIVE {amount} {currency} from {payerName}" or "SEND {amount} {currency} to {receiverName}"
        /// </summary>
        public static string GenerateDocumentDescription(AccountingDocument document, string role)
        {
            var amount = document.Amount;
            var currencyCode = document.CurrencyCode;
            var title = document.Title;

            if (role == "پرداخت کننده" || role == "Payer")
            {
                // Payer receives money (positive transaction)
                var receiverName = document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue
                    ? document.ReceiverCustomer?.FullName ?? "Unknown Customer"
                    : document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue
                        ? $"{document.ReceiverBankAccount?.BankName ?? "Bank"} ({document.ReceiverBankAccount?.AccountNumber ?? ""})"
                        : "System";

                return $"RECEIVE {amount.FormatCurrency(currencyCode)} {currencyCode} from {title} | To: {receiverName}";
            }
            else // Receiver
            {
                // Receiver sends money (negative transaction)
                var payerName = document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue
                    ? document.PayerCustomer?.FullName ?? "Unknown Customer"
                    : document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue
                        ? $"{document.PayerBankAccount?.BankName ?? "Bank"} ({document.PayerBankAccount?.AccountNumber ?? ""})"
                        : "System";

                return $"SEND {amount.FormatCurrency(currencyCode)} {currencyCode} to {title} | From: {payerName}";
            }
        }

        /// <summary>
        /// Generates note for AccountingDocument transaction (without customer info)
        /// Format: "{DocumentType} - Amount: {amount} {currency}"
        /// </summary>
        public static string GenerateDocumentNote(AccountingDocument document)
        {
            // Use Type.ToString() to get English enum name instead of GetDisplayName() which returns Persian
            var documentType = document.Type.ToString();
            var amount = document.Amount;
            var currencyCode = document.CurrencyCode;
            var note = $"{documentType} - Amount: {amount.FormatCurrency(currencyCode)} {currencyCode}";

            if (!string.IsNullOrEmpty(document.ReferenceNumber))
            {
                note += $" | Transaction ID: {document.ReferenceNumber}";
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

