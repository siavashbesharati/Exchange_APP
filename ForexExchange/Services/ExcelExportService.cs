using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Linq;
using ForexExchange.Models;
using ForexExchange.Extensions;

namespace ForexExchange.Services
{
    public class ExcelExportService
    {
        public byte[] GenerateCustomerTimelineExcel(string customerName, List<object> transactions, Dictionary<string, decimal> finalBalances, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();

            // Group transactions by currency
            var transactionsByCurrency = transactions
                .Cast<dynamic>()
                .GroupBy(t => t.CurrencyCode?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key)
                .ToList();

            // Add main summary sheet first
            CreateSummarySheet(package, customerName, finalBalances, fromDate, toDate, transactionsByCurrency.Count);

            // Create a worksheet for each currency
            foreach (var currencyGroup in transactionsByCurrency)
            {
                var currency = currencyGroup.Key;
                var currencyTransactions = currencyGroup.OrderByDescending(t => DateTime.Parse(t.TransactionDate.ToString())).ToList();
                
                CreateCurrencySheet(package, customerName, currency, currencyTransactions, finalBalances.ContainsKey(currency) ? finalBalances[currency] : 0, fromDate, toDate);
            }

            return package.GetAsByteArray();
        }

        private void CreateSummarySheet(ExcelPackage package, string customerName, Dictionary<string, decimal> finalBalances, DateTime? fromDate, DateTime? toDate, int currencyCount)
        {
            var worksheet = package.Workbook.Worksheets.Add("خلاصه گزارش");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "خلاصه گزارش مالی مشتری";
            worksheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            // Customer name
            worksheet.Cells[row, 1].Value = "نام مشتری:";
            worksheet.Cells[row, 2].Value = customerName;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Number of currencies
            worksheet.Cells[row, 1].Value = "تعداد ارزها:";
            worksheet.Cells[row, 2].Value = $"{currencyCount} ارز";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Final balances header
            worksheet.Cells[row, 1].Value = "موجودی نهایی ارزها";
            worksheet.Cells[row, 1, row, 3].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 3], 14, true);
            row++;

            // Balance table header
            worksheet.Cells[row, 1].Value = "ارز";
            worksheet.Cells[row, 2].Value = "موجودی";
            worksheet.Cells[row, 3].Value = "برگه مربوطه";
            StyleHeaderRow(worksheet.Cells[row, 1, row, 3]);
            row++;

            // Final balances
            foreach (var balance in finalBalances.OrderBy(b => b.Key))
            {
                worksheet.Cells[row, 1].Value = balance.Key;
                // Use unified formatting - all IRR values truncate decimals
                worksheet.Cells[row, 2].Value = balance.Value.FormatCurrency("IRR");
                worksheet.Cells[row, 3].Value = $"جزئیات {balance.Key}";
                
                StyleDataRow(worksheet.Cells[row, 1, row, 3]);
                worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private void CreateCurrencySheet(ExcelPackage package, string customerName, string currency, List<dynamic> transactions, decimal finalBalance, DateTime? fromDate, DateTime? toDate)
        {
            var worksheet = package.Workbook.Worksheets.Add($"جزئیات {currency}");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = $"گزارش مالی - {currency}";
            worksheet.Cells[row, 1, row, 6].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 6], 16, true);
            row += 2;

            // Customer name
            worksheet.Cells[row, 1].Value = "نام مشتری:";
            worksheet.Cells[row, 2].Value = customerName;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Currency
            worksheet.Cells[row, 1].Value = "ارز:";
            worksheet.Cells[row, 2].Value = currency;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Final balance
            worksheet.Cells[row, 1].Value = "موجودی نهایی:";
            // Use unified formatting - all IRR values truncate decimals
            worksheet.Cells[row, 2].Value = finalBalance.FormatCurrency("IRR");
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
            row += 2;

            // Transaction count
            worksheet.Cells[row, 1].Value = "تعداد تراکنش:";
            worksheet.Cells[row, 2].Value = $"{transactions.Count} تراکنش";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Transactions header
            worksheet.Cells[row, 1].Value = "تاریخ";
            worksheet.Cells[row, 2].Value = "نوع تراکنش";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "موجودی پس از تراکنش";
            worksheet.Cells[row, 5].Value = "شماره مرجع";
            worksheet.Cells[row, 6].Value = "شرح";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 6]);
            row++;

            // Transaction data
            foreach (var transaction in transactions)
            {
                worksheet.Cells[row, 1].Value = DateTime.Parse(transaction.TransactionDate.ToString()).ToString("yyyy/MM/dd HH:mm");
                worksheet.Cells[row, 2].Value = GetTransactionTypeText(transaction.Type?.ToString());
                worksheet.Cells[row, 3].Value = decimal.Parse(transaction.Amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = decimal.Parse(transaction.RunningBalance?.ToString() ?? "0");
                worksheet.Cells[row, 5].Value = transaction.ReferenceId?.ToString();
                worksheet.Cells[row, 6].Value = transaction.Description?.ToString();

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 6]);
                
                // Apply conditional formatting for transaction type
                StyleTransactionTypeCell(worksheet.Cells[row, 2], transaction.Type?.ToString());
                
                // Format currency columns
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        public byte[] GenerateDocumentsExcel(List<object> documents, DateTime? fromDate = null, DateTime? toDate = null, string? currency = null, string? customer = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();

            // Group documents by currency
            var documentsByCurrency = documents
                .Cast<dynamic>()
                .GroupBy(d => d.currencyCode?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key)
                .ToList();

            // Add main summary sheet first
            CreateDocumentsSummarySheet(package, documentsByCurrency.Count, fromDate, toDate, currency, customer);

            // Create a worksheet for each currency
            foreach (var currencyGroup in documentsByCurrency)
            {
                var currencyCode = currencyGroup.Key;
                var currencyDocuments = currencyGroup.OrderByDescending(d => DateTime.Parse(d.date.ToString())).ToList();
                
                CreateDocumentsCurrencySheet(package, currencyCode, currencyDocuments, fromDate, toDate, customer);
            }

            return package.GetAsByteArray();
        }

        private void CreateDocumentsSummarySheet(ExcelPackage package, int currencyCount, DateTime? fromDate, DateTime? toDate, string? currency, string? customer)
        {
            var worksheet = package.Workbook.Worksheets.Add("خلاصه اسناد");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "خلاصه گزارش اسناد حسابداری";
            worksheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            // Filters applied
            worksheet.Cells[row, 1].Value = "فیلترهای اعمال شده:";
            StyleInfoCell(worksheet.Cells[row, 1]);
            row++;

            if (fromDate.HasValue || toDate.HasValue)
            {
                var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
                var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
                worksheet.Cells[row, 1].Value = "بازه زمانی:";
                worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            if (!string.IsNullOrEmpty(currency))
            {
                worksheet.Cells[row, 1].Value = "ارز:";
                worksheet.Cells[row, 2].Value = currency;
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            if (!string.IsNullOrEmpty(customer))
            {
                worksheet.Cells[row, 1].Value = "مشتری:";
                worksheet.Cells[row, 2].Value = customer;
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            // Number of currencies
            worksheet.Cells[row, 1].Value = "تعداد ارزها:";
            worksheet.Cells[row, 2].Value = $"{currencyCount} ارز";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Currency summary header
            worksheet.Cells[row, 1].Value = "خلاصه ارزها";
            worksheet.Cells[row, 1, row, 3].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 3], 14, true);
            row++;

            // Summary table header
            worksheet.Cells[row, 1].Value = "ارز";
            worksheet.Cells[row, 2].Value = "تعداد اسناد";
            worksheet.Cells[row, 3].Value = "برگه مربوطه";
            StyleHeaderRow(worksheet.Cells[row, 1, row, 3]);
            row++;

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private void CreateDocumentsCurrencySheet(ExcelPackage package, string currency, List<dynamic> documents, DateTime? fromDate, DateTime? toDate, string? customer)
        {
            var worksheet = package.Workbook.Worksheets.Add($"اسناد {currency}");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = $"گزارش اسناد - {currency}";
            worksheet.Cells[row, 1, row, 7].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 7], 16, true);
            row += 2;

            // Currency
            worksheet.Cells[row, 1].Value = "ارز:";
            worksheet.Cells[row, 2].Value = currency;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            if (!string.IsNullOrEmpty(customer))
            {
                worksheet.Cells[row, 1].Value = "مشتری:";
                worksheet.Cells[row, 2].Value = customer;
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            // Document count
            worksheet.Cells[row, 1].Value = "تعداد اسناد:";
            worksheet.Cells[row, 2].Value = $"{documents.Count} سند";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Documents header (removed وضعیت and تاریخ ایجاد, moved شرح to end)
            worksheet.Cells[row, 1].Value = "تاریخ سند";
            worksheet.Cells[row, 2].Value = "نوع سند";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "شماره مرجع";
            worksheet.Cells[row, 5].Value = "پرداخت کننده";
            worksheet.Cells[row, 6].Value = "دریافت کننده";
            worksheet.Cells[row, 7].Value = "شرح";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 7]);
            row++;

            // Document data
            foreach (var document in documents)
            {
                // Add timestamp to document date
                worksheet.Cells[row, 1].Value = DateTime.Parse(document.date.ToString()).ToString("yyyy/MM/dd HH:mm");
                worksheet.Cells[row, 2].Value = document.documentType?.ToString();
                worksheet.Cells[row, 3].Value = decimal.Parse(document.amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = document.referenceNumber?.ToString();
                worksheet.Cells[row, 5].Value = document.payerName?.ToString();
                worksheet.Cells[row, 6].Value = document.receiverName?.ToString();
                worksheet.Cells[row, 7].Value = document.description?.ToString(); // شرح moved to last column

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 7]);
                
                // Format currency column
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        public byte[] GenerateCustomerBankDailyReportExcel(CustomerBankDailyReportViewModel report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();

            var summarySheet = package.Workbook.Worksheets.Add("خلاصه گزارش");
            summarySheet.View.RightToLeft = true;

            var row = 1;

            summarySheet.Cells[row, 1].Value = "گزارش روزانه بانک و مشتری";
            summarySheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(summarySheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            summarySheet.Cells[row, 1].Value = "تاریخ گزارش:";
            summarySheet.Cells[row, 2].Value = report.ReportDate.ToString("yyyy/MM/dd");
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            summarySheet.Cells[row, 1].Value = "تعداد ارزها:";
            summarySheet.Cells[row, 2].Value = report.Currencies.Count;
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            var summary = report.SelectedSummary ?? report.DefaultSummary;

            if (summary != null)
            {
                var numberFormat = summary.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";

                summarySheet.Cells[row, 1].Value = $"جمع موجودی بانک‌ها ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.BankTotal;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                summarySheet.Cells[row, 1].Value = $"جمع موجودی مشتریان ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.CustomerTotal;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                summarySheet.Cells[row, 1].Value = $"تراز بانک + مشتری ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.Difference;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                if (summary.HasMissingRates)
                {
                    summarySheet.Cells[row, 1].Value = "هشدار نرخ تبدیل:";
                    summarySheet.Cells[row, 2].Value = "برخی ارزها بدون نرخ معتبر بوده‌اند";
                    StyleInfoCell(summarySheet.Cells[row, 1]);
                    StyleInfoCell(summarySheet.Cells[row, 2]);
                    row++;
                }
            }
            else
            {
                summarySheet.Cells[row, 1].Value = "نرخ‌های تبدیل معتبر برای خلاصه یافت نشد";
                summarySheet.Cells[row, 1, row, 4].Merge = true;
                summarySheet.Cells[row, 1, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                summarySheet.Cells[row, 1, row, 4].Style.Font.Italic = true;
                row++;
            }

            row++;

            summarySheet.Cells[row, 1].Value = "ارز";
            summarySheet.Cells[row, 2].Value = "مجموع بانک‌ها";
            summarySheet.Cells[row, 3].Value = "مجموع مشتریان";
            summarySheet.Cells[row, 4].Value = "تراز";
            StyleHeaderRow(summarySheet.Cells[row, 1, row, 4]);
            row++;

            if (!report.Currencies.Any())
            {
                summarySheet.Cells[row, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                summarySheet.Cells[row, 1, row, 4].Merge = true;
                summarySheet.Cells[row, 1, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                summarySheet.Cells[row, 1, row, 4].Style.Font.Italic = true;
            }
            else
            {
                foreach (var currency in report.Currencies)
                {
                    summarySheet.Cells[row, 1].Value = $"{currency.CurrencyName} ({currency.CurrencyCode})";
                    summarySheet.Cells[row, 2].Value = (double)currency.BankTotal;
                    summarySheet.Cells[row, 3].Value = (double)currency.CustomerTotal;
                    summarySheet.Cells[row, 4].Value = (double)currency.Difference;

                    var numberFormat = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                    summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                    summarySheet.Cells[row, 3].Style.Numberformat.Format = numberFormat;
                    summarySheet.Cells[row, 4].Style.Numberformat.Format = numberFormat;

                    StyleDataRow(summarySheet.Cells[row, 1, row, 4]);
                    row++;
                }
            }

            summarySheet.Cells[summarySheet.Dimension.Address].AutoFitColumns();

            foreach (var currency in report.Currencies)
            {
                var sheetName = currency.CurrencyCode.Length <= 25 ? currency.CurrencyCode : currency.CurrencyCode.Substring(0, 25);
                var worksheet = package.Workbook.Worksheets.Add(sheetName);
                worksheet.View.RightToLeft = true;

                var rowIndex = 1;

                worksheet.Cells[rowIndex, 1].Value = $"گزارش بانک و مشتری - {currency.CurrencyName} ({currency.CurrencyCode})";
                worksheet.Cells[rowIndex, 1, rowIndex, 6].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 6], 16, true);
                rowIndex += 2;

                worksheet.Cells[rowIndex, 1].Value = "مجموع بانک‌ها:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.BankTotal;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "مجموع مشتریان:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.CustomerTotal;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "تراز:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.Difference;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex += 2;

                worksheet.Cells[rowIndex, 1].Value = "جزئیات بانک‌ها";
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 5], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "بانک";
                worksheet.Cells[rowIndex, 2].Value = "موجودی";
                worksheet.Cells[rowIndex, 3].Value = "آخرین تراکنش";
                worksheet.Cells[rowIndex, 4].Value = "شماره حساب";
                worksheet.Cells[rowIndex, 5].Value = "صاحب حساب";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                rowIndex++;

                if (!currency.BankDetails.Any())
                {
                    worksheet.Cells[rowIndex, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.Font.Italic = true;
                    rowIndex++;
                }
                else
                {
                    foreach (var bank in currency.BankDetails)
                    {
                        worksheet.Cells[rowIndex, 1].Value = bank.BankName;
                        worksheet.Cells[rowIndex, 2].Value = (double)bank.Balance;
                        worksheet.Cells[rowIndex, 3].Value = bank.LastTransactionAt.ToString("yyyy/MM/dd HH:mm");
                        worksheet.Cells[rowIndex, 4].Value = bank.AccountNumber;
                        worksheet.Cells[rowIndex, 5].Value = bank.OwnerName;

                        worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                        StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                        rowIndex++;
                    }
                }

                rowIndex++;
                worksheet.Cells[rowIndex, 1].Value = "جزئیات مشتریان";
                worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 4], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "مشتری";
                worksheet.Cells[rowIndex, 2].Value = "موجودی";
                worksheet.Cells[rowIndex, 3].Value = "آخرین تراکنش";
                worksheet.Cells[rowIndex, 4].Value = "شناسه مشتری";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 4]);
                rowIndex++;

                if (!currency.CustomerDetails.Any())
                {
                    worksheet.Cells[rowIndex, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Style.Font.Italic = true;
                }
                else
                {
                    foreach (var customer in currency.CustomerDetails)
                    {
                        worksheet.Cells[rowIndex, 1].Value = customer.CustomerName;
                        worksheet.Cells[rowIndex, 2].Value = (double)customer.Balance;
                        worksheet.Cells[rowIndex, 3].Value = customer.LastTransactionAt.ToString("yyyy/MM/dd HH:mm");
                        worksheet.Cells[rowIndex, 4].Value = customer.CustomerId;

                        worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                        StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 4]);
                        rowIndex++;
                    }
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }

            return package.GetAsByteArray();
        }

        public byte[] GenerateCustomerBankHistoryReportExcel(CustomerBankHistoryReportViewModel report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();

            var summarySheet = package.Workbook.Worksheets.Add("خلاصه گزارش");
            summarySheet.View.RightToLeft = true;

            var row = 1;

            summarySheet.Cells[row, 1].Value = "گزارش تاریخچه بانک و مشتری";
            summarySheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(summarySheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            summarySheet.Cells[row, 1].Value = "از تاریخ:";
            summarySheet.Cells[row, 2].Value = report.DateFrom.ToString("yyyy/MM/dd");
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            summarySheet.Cells[row, 1].Value = "تا تاریخ:";
            summarySheet.Cells[row, 2].Value = report.DateTo.ToString("yyyy/MM/dd");
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            summarySheet.Cells[row, 1].Value = "تعداد ارزها:";
            summarySheet.Cells[row, 2].Value = report.Currencies.Count;
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            var summary = report.SelectedSummary ?? report.DefaultSummary;

            if (summary != null)
            {
                var numberFormat = summary.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";

                summarySheet.Cells[row, 1].Value = $"جمع موجودی بانک‌ها ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.BankTotal;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                summarySheet.Cells[row, 1].Value = $"جمع موجودی مشتریان ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.CustomerTotal;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                summarySheet.Cells[row, 1].Value = $"تراز بانک + مشتری ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.Difference;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                if (summary.HasMissingRates)
                {
                    summarySheet.Cells[row, 1].Value = "هشدار نرخ تبدیل:";
                    summarySheet.Cells[row, 2].Value = "برخی ارزها بدون نرخ معتبر بوده‌اند";
                    StyleInfoCell(summarySheet.Cells[row, 1]);
                    StyleInfoCell(summarySheet.Cells[row, 2]);
                    row++;
                }
            }
            else
            {
                summarySheet.Cells[row, 1].Value = "نرخ‌های تبدیل معتبر برای خلاصه یافت نشد";
                summarySheet.Cells[row, 1, row, 4].Merge = true;
                summarySheet.Cells[row, 1, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                summarySheet.Cells[row, 1, row, 4].Style.Font.Italic = true;
                row++;
            }

            row++;

            summarySheet.Cells[row, 1].Value = "ارز";
            summarySheet.Cells[row, 2].Value = "مجموع بانک‌ها";
            summarySheet.Cells[row, 3].Value = "مجموع مشتریان";
            summarySheet.Cells[row, 4].Value = "تراز";
            StyleHeaderRow(summarySheet.Cells[row, 1, row, 4]);
            row++;

            if (!report.Currencies.Any())
            {
                summarySheet.Cells[row, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                summarySheet.Cells[row, 1, row, 4].Merge = true;
                summarySheet.Cells[row, 1, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                summarySheet.Cells[row, 1, row, 4].Style.Font.Italic = true;
            }
            else
            {
                foreach (var currency in report.Currencies)
                {
                    summarySheet.Cells[row, 1].Value = $"{currency.CurrencyName} ({currency.CurrencyCode})";
                    summarySheet.Cells[row, 2].Value = (double)currency.BankTotal;
                    summarySheet.Cells[row, 3].Value = (double)currency.CustomerTotal;
                    summarySheet.Cells[row, 4].Value = (double)currency.Difference;

                    var numberFormat = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                    summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                    summarySheet.Cells[row, 3].Style.Numberformat.Format = numberFormat;
                    summarySheet.Cells[row, 4].Style.Numberformat.Format = numberFormat;

                    StyleDataRow(summarySheet.Cells[row, 1, row, 4]);
                    row++;
                }
            }

            summarySheet.Cells[summarySheet.Dimension.Address].AutoFitColumns();

            foreach (var currency in report.Currencies)
            {
                var sheetName = currency.CurrencyCode.Length <= 25 ? currency.CurrencyCode : currency.CurrencyCode.Substring(0, 25);
                var worksheet = package.Workbook.Worksheets.Add(sheetName);
                worksheet.View.RightToLeft = true;

                var rowIndex = 1;

                worksheet.Cells[rowIndex, 1].Value = $"گزارش تاریخچه بانک و مشتری - {currency.CurrencyName} ({currency.CurrencyCode})";
                worksheet.Cells[rowIndex, 1, rowIndex, 6].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 6], 16, true);
                rowIndex += 2;

                worksheet.Cells[rowIndex, 1].Value = "مجموع بانک‌ها:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.BankTotal;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "مجموع مشتریان:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.CustomerTotal;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "تراز:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.Difference;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex += 2;

                worksheet.Cells[rowIndex, 1].Value = "جزئیات بانک‌ها";
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 5], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "بانک";
                worksheet.Cells[rowIndex, 2].Value = "موجودی";
                worksheet.Cells[rowIndex, 3].Value = "آخرین تراکنش";
                worksheet.Cells[rowIndex, 4].Value = "شماره حساب";
                worksheet.Cells[rowIndex, 5].Value = "صاحب حساب";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                rowIndex++;

                if (!currency.BankDetails.Any())
                {
                    worksheet.Cells[rowIndex, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.Font.Italic = true;
                    rowIndex++;
                }
                else
                {
                    foreach (var bank in currency.BankDetails)
                    {
                        worksheet.Cells[rowIndex, 1].Value = bank.BankName;
                        worksheet.Cells[rowIndex, 2].Value = (double)bank.Balance;
                        worksheet.Cells[rowIndex, 3].Value = bank.LastTransactionAt.ToString("yyyy/MM/dd HH:mm");
                        worksheet.Cells[rowIndex, 4].Value = bank.AccountNumber;
                        worksheet.Cells[rowIndex, 5].Value = bank.OwnerName;

                        worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                        StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                        rowIndex++;
                    }
                }

                rowIndex++;
                worksheet.Cells[rowIndex, 1].Value = "جزئیات مشتریان";
                worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 4], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "مشتری";
                worksheet.Cells[rowIndex, 2].Value = "موجودی";
                worksheet.Cells[rowIndex, 3].Value = "آخرین تراکنش";
                worksheet.Cells[rowIndex, 4].Value = "شناسه مشتری";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 4]);
                rowIndex++;

                if (!currency.CustomerDetails.Any())
                {
                    worksheet.Cells[rowIndex, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Style.Font.Italic = true;
                }
                else
                {
                    foreach (var customer in currency.CustomerDetails)
                    {
                        worksheet.Cells[rowIndex, 1].Value = customer.CustomerName;
                        worksheet.Cells[rowIndex, 2].Value = (double)customer.Balance;
                        worksheet.Cells[rowIndex, 3].Value = customer.LastTransactionAt.ToString("yyyy/MM/dd HH:mm");
                        worksheet.Cells[rowIndex, 4].Value = customer.CustomerId;

                        worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                        StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 4]);
                        rowIndex++;
                    }
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }

            return package.GetAsByteArray();
        }

        public byte[] GenerateExpensesReportExcel(ExpensesReportViewModel report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();

            var summarySheet = package.Workbook.Worksheets.Add("خلاصه گزارش");
            summarySheet.View.RightToLeft = true;

            var row = 1;

            summarySheet.Cells[row, 1].Value = "گزارش هزینه‌ها";
            summarySheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(summarySheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            summarySheet.Cells[row, 1].Value = "از تاریخ:";
            summarySheet.Cells[row, 2].Value = report.DateFrom.ToString("yyyy/MM/dd");
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            summarySheet.Cells[row, 1].Value = "تا تاریخ:";
            summarySheet.Cells[row, 2].Value = report.DateTo.ToString("yyyy/MM/dd");
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            summarySheet.Cells[row, 1].Value = "تعداد ارزها:";
            summarySheet.Cells[row, 2].Value = report.Currencies.Count;
            StyleInfoCell(summarySheet.Cells[row, 1]);
            StyleInfoCell(summarySheet.Cells[row, 2]);
            row++;

            var summary = report.SelectedSummary ?? report.DefaultSummary;

            if (summary != null)
            {
                var numberFormat = summary.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";

                summarySheet.Cells[row, 1].Value = $"جمع موجودی بانک‌ها ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.BankTotal;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                summarySheet.Cells[row, 1].Value = $"جمع موجودی سهامداران ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.CustomerTotal;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                summarySheet.Cells[row, 1].Value = $"تراز بانک + سهامداران ({summary.CurrencyCode}):";
                summarySheet.Cells[row, 2].Value = (double)summary.Difference;
                StyleInfoCell(summarySheet.Cells[row, 1]);
                StyleInfoCell(summarySheet.Cells[row, 2]);
                summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                row++;

                if (summary.HasMissingRates)
                {
                    summarySheet.Cells[row, 1].Value = "هشدار نرخ تبدیل:";
                    summarySheet.Cells[row, 2].Value = "برخی ارزها بدون نرخ معتبر بوده‌اند";
                    StyleInfoCell(summarySheet.Cells[row, 1]);
                    StyleInfoCell(summarySheet.Cells[row, 2]);
                    row++;
                }
            }
            else
            {
                summarySheet.Cells[row, 1].Value = "نرخ‌های تبدیل معتبر برای خلاصه یافت نشد";
                summarySheet.Cells[row, 1, row, 4].Merge = true;
                summarySheet.Cells[row, 1, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                summarySheet.Cells[row, 1, row, 4].Style.Font.Italic = true;
                row++;
            }

            row++;

            summarySheet.Cells[row, 1].Value = "ارز";
            summarySheet.Cells[row, 2].Value = "مجموع بانک‌ها";
            summarySheet.Cells[row, 3].Value = "مجموع سهامداران";
            summarySheet.Cells[row, 4].Value = "تراز";
            StyleHeaderRow(summarySheet.Cells[row, 1, row, 4]);
            row++;

            if (!report.Currencies.Any())
            {
                summarySheet.Cells[row, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                summarySheet.Cells[row, 1, row, 4].Merge = true;
                summarySheet.Cells[row, 1, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                summarySheet.Cells[row, 1, row, 4].Style.Font.Italic = true;
            }
            else
            {
                foreach (var currency in report.Currencies)
                {
                    summarySheet.Cells[row, 1].Value = $"{currency.CurrencyName} ({currency.CurrencyCode})";
                    summarySheet.Cells[row, 2].Value = (double)currency.BankTotal;
                    summarySheet.Cells[row, 3].Value = (double)currency.CustomerTotal;
                    summarySheet.Cells[row, 4].Value = (double)currency.Difference;

                    var numberFormat = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                    summarySheet.Cells[row, 2].Style.Numberformat.Format = numberFormat;
                    summarySheet.Cells[row, 3].Style.Numberformat.Format = numberFormat;
                    summarySheet.Cells[row, 4].Style.Numberformat.Format = numberFormat;

                    StyleDataRow(summarySheet.Cells[row, 1, row, 4]);
                    row++;
                }
            }

            summarySheet.Cells[summarySheet.Dimension.Address].AutoFitColumns();

            foreach (var currency in report.Currencies)
            {
                var sheetName = currency.CurrencyCode.Length <= 25 ? currency.CurrencyCode : currency.CurrencyCode.Substring(0, 25);
                var worksheet = package.Workbook.Worksheets.Add(sheetName);
                worksheet.View.RightToLeft = true;

                var rowIndex = 1;

                worksheet.Cells[rowIndex, 1].Value = $"گزارش هزینه‌ها - {currency.CurrencyName} ({currency.CurrencyCode})";
                worksheet.Cells[rowIndex, 1, rowIndex, 6].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 6], 16, true);
                rowIndex += 2;

                worksheet.Cells[rowIndex, 1].Value = "مجموع بانک‌ها:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.BankTotal;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "مجموع سهامداران:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.CustomerTotal;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "تراز:";
                worksheet.Cells[rowIndex, 2].Value = (double)currency.Difference;
                StyleInfoCell(worksheet.Cells[rowIndex, 1]);
                StyleInfoCell(worksheet.Cells[rowIndex, 2]);
                worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                rowIndex += 2;

                worksheet.Cells[rowIndex, 1].Value = "جزئیات بانک‌ها";
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 5], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "بانک";
                worksheet.Cells[rowIndex, 2].Value = "موجودی";
                worksheet.Cells[rowIndex, 3].Value = "آخرین تراکنش";
                worksheet.Cells[rowIndex, 4].Value = "شماره حساب";
                worksheet.Cells[rowIndex, 5].Value = "صاحب حساب";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                rowIndex++;

                if (!currency.BankDetails.Any())
                {
                    worksheet.Cells[rowIndex, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.Font.Italic = true;
                    rowIndex++;
                }
                else
                {
                    foreach (var bank in currency.BankDetails)
                    {
                        worksheet.Cells[rowIndex, 1].Value = bank.BankName;
                        worksheet.Cells[rowIndex, 2].Value = (double)bank.Balance;
                        worksheet.Cells[rowIndex, 3].Value = bank.LastTransactionAt.ToString("yyyy/MM/dd HH:mm");
                        worksheet.Cells[rowIndex, 4].Value = bank.AccountNumber;
                        worksheet.Cells[rowIndex, 5].Value = bank.OwnerName;

                        worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                        StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                        rowIndex++;
                    }
                }

                rowIndex++;
                worksheet.Cells[rowIndex, 1].Value = "جزئیات سهامداران";
                worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 4], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "سهامدار";
                worksheet.Cells[rowIndex, 2].Value = "موجودی";
                worksheet.Cells[rowIndex, 3].Value = "آخرین تراکنش";
                worksheet.Cells[rowIndex, 4].Value = "شناسه مشتری";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 4]);
                rowIndex++;

                if (!currency.CustomerDetails.Any())
                {
                    worksheet.Cells[rowIndex, 1].Value = "داده‌ای برای نمایش وجود ندارد";
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[rowIndex, 1, rowIndex, 4].Style.Font.Italic = true;
                }
                else
                {
                    foreach (var customer in currency.CustomerDetails)
                    {
                        worksheet.Cells[rowIndex, 1].Value = customer.CustomerName;
                        worksheet.Cells[rowIndex, 2].Value = (double)customer.Balance;
                        worksheet.Cells[rowIndex, 3].Value = customer.LastTransactionAt.ToString("yyyy/MM/dd HH:mm");
                        worksheet.Cells[rowIndex, 4].Value = customer.CustomerId;

                        worksheet.Cells[rowIndex, 2].Style.Numberformat.Format = currency.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";
                        StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 4]);
                        rowIndex++;
                    }
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }

            return package.GetAsByteArray();
        }

        public byte[] GeneratePoolTimelineExcel(string currencyCode, List<object> transactions, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();

            // Handle empty transactions list
            if (transactions == null || !transactions.Any())
            {
                var worksheet = package.Workbook.Worksheets.Add("گزارش داشبورد");
                worksheet.View.RightToLeft = true;
                worksheet.Cells[1, 1].Value = "گزارش داشبورد";
                worksheet.Cells[1, 1, 1, 10].Merge = true;
                StyleHeaderCell(worksheet.Cells[1, 1, 1, 10], 16, true);
                worksheet.Cells[3, 1].Value = "هیچ تراکنشی یافت نشد";
                worksheet.Cells[3, 1, 3, 10].Merge = true;
                if (worksheet.Dimension != null)
                {
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                }
                return package.GetAsByteArray();
            }

            // Group transactions by CurrencyCode if there are multiple currencies
            var transactionsByCurrency = transactions
                .Cast<dynamic>()
                .GroupBy(t => t.CurrencyCode?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key)
                .ToList();

            bool isMultipleCurrencies = transactionsByCurrency.Count > 1 || currencyCode == "همه";

            // Calculate summary data
            var summaryData = new List<(string Currency, int Count, decimal SumAmount, decimal FinalBalance)>();
            foreach (var currencyGroup in transactionsByCurrency)
            {
                var currency = currencyGroup.Key;
                var currencyTransactions = currencyGroup.ToList();
                decimal sumAmount = 0;
                decimal finalBalance = 0;

                foreach (dynamic t in currencyTransactions)
                {
                    try
                    {
                        decimal amount = Convert.ToDecimal(t.Amount ?? 0);
                        sumAmount += amount;
                        finalBalance = Convert.ToDecimal(t.Balance ?? 0);
                    }
                    catch { }
                }

                summaryData.Add((currency, currencyTransactions.Count, sumAmount, finalBalance));
            }

            // Create summary sheet first
            CreatePoolSummarySheet(package, summaryData, fromDate, toDate, currencyCode, isMultipleCurrencies);

            // Create a worksheet for each currency
            foreach (var currencyGroup in transactionsByCurrency)
            {
                var currency = currencyGroup.Key;
                var currencyTransactions = currencyGroup.OrderBy(t =>
                {
                    try
                    {
                        var dateStr = t.Date?.ToString() ?? "";
                        var timeStr = t.Time?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(dateStr) && !string.IsNullOrEmpty(timeStr))
                        {
                            return DateTime.Parse($"{dateStr} {timeStr}");
                        }
                        return DateTime.MinValue;
                    }
                    catch
                    {
                        return DateTime.MinValue;
                    }
                }).ToList();

                CreatePoolCurrencySheet(package, currency, currencyTransactions, fromDate, toDate, isMultipleCurrencies);
            }

            return package.GetAsByteArray();
        }

        private void CreatePoolSummarySheet(ExcelPackage package, List<(string Currency, int Count, decimal SumAmount, decimal FinalBalance)> summaryData, DateTime? fromDate, DateTime? toDate, string currencyCode, bool isMultipleCurrencies)
        {
            var worksheet = package.Workbook.Worksheets.Add("خلاصه گزارش");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title - Colorful gradient style
            worksheet.Cells[row, 1].Value = "خلاصه گزارش داشبورد ارزی";
            worksheet.Cells[row, 1, row, 5].Merge = true;
            StyleHeaderCellColorful(worksheet.Cells[row, 1, row, 5], 18, true, Color.FromArgb(68, 114, 196)); // Blue
            row += 2;

            // Report info
            worksheet.Cells[row, 1].Value = "ارز انتخاب شده:";
            worksheet.Cells[row, 2].Value = string.IsNullOrEmpty(currencyCode) || currencyCode == "همه" ? "همه ارزها" : currencyCode;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCellBordered(worksheet.Cells[row, 2], Color.FromArgb(217, 225, 242)); // Light blue background
            row++;

            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCellBordered(worksheet.Cells[row, 2], Color.FromArgb(217, 225, 242));
            row++;

            worksheet.Cells[row, 1].Value = "تعداد ارزها:";
            worksheet.Cells[row, 2].Value = $"{summaryData.Count} ارز";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCellBordered(worksheet.Cells[row, 2], Color.FromArgb(217, 225, 242));
            row += 2;

            // Summary table header - Colorful
            worksheet.Cells[row, 1].Value = "خلاصه ارزها";
            worksheet.Cells[row, 1, row, 5].Merge = true;
            StyleHeaderCellColorful(worksheet.Cells[row, 1, row, 5], 14, true, Color.FromArgb(112, 173, 71)); // Green
            row++;

            // Table headers
            worksheet.Cells[row, 1].Value = "ارز";
            worksheet.Cells[row, 2].Value = "تعداد تراکنش";
            worksheet.Cells[row, 3].Value = "جمع مبلغ";
            worksheet.Cells[row, 4].Value = "موجودی نهایی";
            worksheet.Cells[row, 5].Value = "برگه جزئیات";
            StyleHeaderRowColorful(worksheet.Cells[row, 1, row, 5], Color.FromArgb(68, 114, 196)); // Blue header
            row++;

            // Summary data rows with alternating colors
            int dataRowIndex = 0;
            foreach (var summary in summaryData)
            {
                worksheet.Cells[row, 1].Value = summary.Currency;
                worksheet.Cells[row, 2].Value = summary.Count;
                worksheet.Cells[row, 3].Value = summary.SumAmount;
                worksheet.Cells[row, 4].Value = summary.FinalBalance;
                worksheet.Cells[row, 5].Value = isMultipleCurrencies ? $"داشبورد {summary.Currency}" : "گزارش داشبورد";
                
                // Alternate row colors
                var bgColor = dataRowIndex % 2 == 0 ? Color.FromArgb(242, 242, 242) : Color.White;
                StyleDataRowColorful(worksheet.Cells[row, 1, row, 5], bgColor);
                
                // Format number columns
                worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                
                row++;
                dataRowIndex++;
            }

            // Total row
            if (summaryData.Count > 1)
            {
                worksheet.Cells[row, 1].Value = "جمع کل";
                worksheet.Cells[row, 2].Value = summaryData.Sum(s => s.Count);
                worksheet.Cells[row, 3].Value = summaryData.Sum(s => s.SumAmount);
                worksheet.Cells[row, 4].Value = ""; // Final balance doesn't sum across currencies
                worksheet.Cells[row, 5].Value = "";
                
                StyleHeaderRowColorful(worksheet.Cells[row, 1, row, 5], Color.FromArgb(255, 192, 0)); // Orange/Gold for total
                worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 2].Style.Font.Bold = true;
                worksheet.Cells[row, 3].Style.Font.Bold = true;
            }

            // Auto-fit columns
            if (worksheet.Dimension != null)
            {
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }
        }

        private void CreatePoolCurrencySheet(ExcelPackage package, string currency, List<dynamic> currencyTransactions, DateTime? fromDate, DateTime? toDate, bool isMultipleCurrencies)
        {
            var worksheet = package.Workbook.Worksheets.Add(isMultipleCurrencies ? $"داشبورد {currency}" : "گزارش داشبورد");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title - Colorful
            worksheet.Cells[row, 1].Value = "گزارش داشبورد ارزی";
            worksheet.Cells[row, 1, row, 10].Merge = true;
            StyleHeaderCellColorful(worksheet.Cells[row, 1, row, 10], 16, true, Color.FromArgb(68, 114, 196)); // Blue
            row += 2;

            // Currency
            worksheet.Cells[row, 1].Value = "ارز:";
            worksheet.Cells[row, 2].Value = currency;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCellBordered(worksheet.Cells[row, 2], Color.FromArgb(217, 225, 242)); // Light blue
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCellBordered(worksheet.Cells[row, 2], Color.FromArgb(217, 225, 242));
            row += 2;

            // Transactions header with row number column
            worksheet.Cells[row, 1].Value = "#";
            worksheet.Cells[row, 2].Value = "تاریخ";
            worksheet.Cells[row, 3].Value = "نوع تراکنش";
            worksheet.Cells[row, 4].Value = "مبلغ";
            worksheet.Cells[row, 5].Value = "شرح";
            worksheet.Cells[row, 6].Value = "موجودی پس از تراکنش";
            worksheet.Cells[row, 7].Value = "شماره مرجع";
            worksheet.Cells[row, 8].Value = "وضعیت";
            worksheet.Cells[row, 9].Value = "مشتری";
            worksheet.Cells[row, 10].Value = "ارز مقابل";

            StyleHeaderRowColorful(worksheet.Cells[row, 1, row, 10], Color.FromArgb(68, 114, 196)); // Blue header
            row++;

            int rowNumber = 1;
            decimal totalAmount = 0;
            int dataRowIndex = 0;

            // Transaction data - use PascalCase property names
            foreach (dynamic transaction in currencyTransactions)
            {
                try
                {
                    var dateStr = transaction.Date?.ToString() ?? "";
                    
                    decimal amount = Convert.ToDecimal(transaction.Amount ?? 0);
                    totalAmount += amount;
                    
                    worksheet.Cells[row, 1].Value = rowNumber; // Row number
                    worksheet.Cells[row, 2].Value = dateStr; // Date is already in yyyy/MM/dd format
                    worksheet.Cells[row, 3].Value = transaction.TransactionType?.ToString() ?? "";
                    worksheet.Cells[row, 4].Value = amount;
                    worksheet.Cells[row, 5].Value = transaction.Description?.ToString() ?? "";
                    worksheet.Cells[row, 6].Value = transaction.Balance ?? 0;
                    worksheet.Cells[row, 7].Value = transaction.ReferenceId?.ToString() ?? "";
                    worksheet.Cells[row, 8].Value = "تایید شده";
                    worksheet.Cells[row, 9].Value = transaction.CustomerName?.ToString() ?? "";
                    worksheet.Cells[row, 10].Value = transaction.PairedCurrencyCode?.ToString() ?? "";

                    // Alternate row colors for better readability
                    var bgColor = dataRowIndex % 2 == 0 ? Color.FromArgb(242, 242, 242) : Color.White;
                    StyleDataRowColorful(worksheet.Cells[row, 1, row, 10], bgColor);
                    
                    // Format number columns
                    worksheet.Cells[row, 1].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0";
                    
                    row++;
                    rowNumber++;
                    dataRowIndex++;
                }
                catch (Exception ex)
                {
                    // Skip problematic transactions
                    continue;
                }
            }

            // Add total row at the bottom
            worksheet.Cells[row, 1].Value = "";
            worksheet.Cells[row, 2].Value = "";
            worksheet.Cells[row, 3].Value = "جمع کل:";
            worksheet.Cells[row, 4].Value = totalAmount;
            worksheet.Cells[row, 5].Value = "";
            worksheet.Cells[row, 6].Value = "";
            worksheet.Cells[row, 7].Value = "";
            worksheet.Cells[row, 8].Value = "";
            worksheet.Cells[row, 9].Value = "";
            worksheet.Cells[row, 10].Value = "";

            // Style total row with gold/orange color
            StyleHeaderRowColorful(worksheet.Cells[row, 1, row, 10], Color.FromArgb(255, 192, 0)); // Gold/Orange
            worksheet.Cells[row, 3].Style.Font.Bold = true;
            worksheet.Cells[row, 4].Style.Font.Bold = true;
            worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";

            // Auto-fit columns
            if (worksheet.Dimension != null)
            {
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }
        }

        private void StyleHeaderCellColorful(ExcelRange range, int fontSize, bool bold, Color backgroundColor)
        {
            range.Style.Font.Size = fontSize;
            range.Style.Font.Bold = bold;
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(backgroundColor);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.Black);
        }

        private void StyleHeaderRowColorful(ExcelRange range, Color backgroundColor)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(backgroundColor);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.Black);
            
            foreach (var cell in range)
            {
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.Black);
            }
        }

        private void StyleDataRowColorful(ExcelRange range, Color backgroundColor)
        {
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(backgroundColor);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(217, 217, 217));
            
            foreach (var cell in range)
            {
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(217, 217, 217));
            }
        }

        private void StyleInfoCellBordered(ExcelRange cell, Color backgroundColor)
        {
            cell.Style.Font.Bold = true;
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(backgroundColor);
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(217, 217, 217));
        }

        public byte[] GenerateBankAccountTimelineExcel(string bankAccountName, List<object> transactions, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("گزارش حساب بانکی");

            // Set worksheet direction to RTL
            worksheet.View.RightToLeft = true;

            // Add header information
            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "گزارش حساب بانکی";
            worksheet.Cells[row, 1, row, 8].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 8], 16, true);
            row += 2;

            // Bank Account
            worksheet.Cells[row, 1].Value = "حساب بانکی:";
            worksheet.Cells[row, 2].Value = bankAccountName;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Transactions header
            worksheet.Cells[row, 1].Value = "تاریخ";
            worksheet.Cells[row, 2].Value = "نوع تراکنش";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "شرح";
            worksheet.Cells[row, 5].Value = "موجودی پس از تراکنش";
            worksheet.Cells[row, 6].Value = "شماره مرجع";
            worksheet.Cells[row, 7].Value = "وضعیت";
            worksheet.Cells[row, 8].Value = "یادداشت";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 8]);
            row++;

            // Transaction data
            foreach (dynamic transaction in transactions)
            {
                worksheet.Cells[row, 1].Value = DateTime.Parse(transaction.date.ToString()).ToString("yyyy/MM/dd");
                worksheet.Cells[row, 2].Value = transaction.type?.ToString();
                worksheet.Cells[row, 3].Value = decimal.Parse(transaction.amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = transaction.description?.ToString();
                worksheet.Cells[row, 5].Value = decimal.Parse(transaction.runningBalance?.ToString() ?? "0");
                worksheet.Cells[row, 6].Value = transaction.referenceId?.ToString();
                worksheet.Cells[row, 7].Value = "تایید شده";
                worksheet.Cells[row, 8].Value = transaction.note?.ToString();

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 8]);
                
                // Format currency columns
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        public byte[] GenerateOrdersExcel(List<object> orders, DateTime? fromDate = null, DateTime? toDate = null, string? fromCurrency = null, string? toCurrency = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("گزارش معاملات");

            // Set worksheet direction to RTL
            worksheet.View.RightToLeft = true;

            // Add header information
            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "گزارش معاملات";
            worksheet.Cells[row, 1, row, 8].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 8], 16, true);
            row += 2;

            // Filters applied
            if (fromDate.HasValue || toDate.HasValue || !string.IsNullOrEmpty(fromCurrency) || !string.IsNullOrEmpty(toCurrency))
            {
                worksheet.Cells[row, 1].Value = "فیلترهای اعمال شده:";
                StyleInfoCell(worksheet.Cells[row, 1]);
                row++;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
                    var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
                    worksheet.Cells[row, 1].Value = "بازه زمانی:";
                    worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }

                if (!string.IsNullOrEmpty(fromCurrency))
                {
                    worksheet.Cells[row, 1].Value = "ارز مبدأ:";
                    worksheet.Cells[row, 2].Value = fromCurrency;
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }

                if (!string.IsNullOrEmpty(toCurrency))
                {
                    worksheet.Cells[row, 1].Value = "ارز مقصد:";
                    worksheet.Cells[row, 2].Value = toCurrency;
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }
                row++;
            }

            // Orders header
            worksheet.Cells[row, 1].Value = "شناسه";
            worksheet.Cells[row, 2].Value = "تاریخ";
            worksheet.Cells[row, 3].Value = "مشتری";
            worksheet.Cells[row, 4].Value = "از ارز";
            worksheet.Cells[row, 5].Value = "مبلغ";
            worksheet.Cells[row, 6].Value = "به ارز";
            worksheet.Cells[row, 7].Value = "نرخ تبدیل";
            worksheet.Cells[row, 8].Value = "مبلغ نهایی";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 8]);
            row++;

            // Order data
            foreach (dynamic order in orders)
            {
                worksheet.Cells[row, 1].Value = order.id?.ToString();
                worksheet.Cells[row, 2].Value = DateTime.Parse(order.createdAt.ToString()).ToString("yyyy/MM/dd HH:mm");
                worksheet.Cells[row, 3].Value = order.customerName?.ToString();
                worksheet.Cells[row, 4].Value = order.fromCurrency?.ToString();
                worksheet.Cells[row, 5].Value = decimal.Parse(order.amount?.ToString() ?? "0");
                worksheet.Cells[row, 6].Value = order.toCurrency?.ToString();
                worksheet.Cells[row, 7].Value = decimal.Parse(order.rate?.ToString() ?? "0");
                worksheet.Cells[row, 8].Value = decimal.Parse(order.totalValue?.ToString() ?? "0");

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 8]);
                
                // Format currency columns
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        public byte[] GenerateAllCustomersBalancesExcel(AllCustomersBalanceReportData reportData)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("تراز همه مشتریان");
            worksheet.View.RightToLeft = true;

            var data = reportData?.Customers ?? new List<AllCustomerBalancePrintViewModel>();
            var summary = reportData?.Summary ?? new AllCustomersBalanceSummary();
            var rowIndex = 1;

            worksheet.Cells[rowIndex, 1].Value = "خلاصه گزارش";
            worksheet.Cells[rowIndex, 1, rowIndex, 4].Merge = true;
            StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 4], 16, true);
            rowIndex += 2;

            worksheet.Cells[rowIndex, 1].Value = "کل مشتریان با تراز";
            worksheet.Cells[rowIndex, 2].Value = summary.TotalCustomersWithBalances;
            StyleInfoCell(worksheet.Cells[rowIndex, 1]);
            StyleInfoCell(worksheet.Cells[rowIndex, 2]);
            rowIndex++;

            worksheet.Cells[rowIndex, 1].Value = "مشتریان بستانکار";
            worksheet.Cells[rowIndex, 2].Value = summary.TotalCustomersWithCredit;
            StyleInfoCell(worksheet.Cells[rowIndex, 1]);
            StyleInfoCell(worksheet.Cells[rowIndex, 2]);
            rowIndex++;

            worksheet.Cells[rowIndex, 1].Value = "مشتریان بدهکار";
            worksheet.Cells[rowIndex, 2].Value = summary.TotalCustomersWithDebt;
            StyleInfoCell(worksheet.Cells[rowIndex, 1]);
            StyleInfoCell(worksheet.Cells[rowIndex, 2]);
            rowIndex += 2;

            if (summary.CurrencyTotals.Any())
            {
                worksheet.Cells[rowIndex, 1].Value = "خلاصه به تفکیک ارز";
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                StyleHeaderCell(worksheet.Cells[rowIndex, 1, rowIndex, 5], 14, true);
                rowIndex++;

                worksheet.Cells[rowIndex, 1].Value = "ارز";
                worksheet.Cells[rowIndex, 2].Value = "تعداد مشتری";
                worksheet.Cells[rowIndex, 3].Value = "جمع بستانکار";
                worksheet.Cells[rowIndex, 4].Value = "جمع بدهکار";
                worksheet.Cells[rowIndex, 5].Value = "تراز";
                StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                rowIndex++;

                foreach (var entry in summary.CurrencyTotals.OrderBy(e => e.Key))
                {
                    worksheet.Cells[rowIndex, 1].Value = entry.Key;
                    worksheet.Cells[rowIndex, 2].Value = entry.Value.CustomerCount;
                    worksheet.Cells[rowIndex, 3].Value = entry.Value.TotalCredit;
                    worksheet.Cells[rowIndex, 4].Value = entry.Value.TotalDebt > 0 ? -entry.Value.TotalDebt : 0;
                    worksheet.Cells[rowIndex, 5].Value = entry.Value.NetBalance;

                    var creditFormat = entry.Key == "IRR" ? "#,##0" : "#,##0.00";
                    worksheet.Cells[rowIndex, 3].Style.Numberformat.Format = creditFormat;
                    worksheet.Cells[rowIndex, 4].Style.Numberformat.Format = creditFormat;
                    worksheet.Cells[rowIndex, 5].Style.Numberformat.Format = creditFormat;

                    StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
                    rowIndex++;
                }

                rowIndex++;
            }

            worksheet.Cells[rowIndex, 1].Value = "#";
            worksheet.Cells[rowIndex, 2].Value = "نام مشتری";
            worksheet.Cells[rowIndex, 3].Value = "کد مشتری";
            worksheet.Cells[rowIndex, 4].Value = "ارز";
            worksheet.Cells[rowIndex, 5].Value = "تراز";
            StyleHeaderRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);
            rowIndex++;

            if (!data.Any())
            {
                worksheet.Cells[rowIndex, 1].Value = "هیچ داده‌ای موجود نیست";
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Merge = true;
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[rowIndex, 1, rowIndex, 5].Style.Font.Italic = true;
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                return package.GetAsByteArray();
            }

            var index = 1;

            foreach (var customer in data)
            {
                foreach (var balance in customer.Balances)
                {
                    worksheet.Cells[rowIndex, 1].Value = index;
                    worksheet.Cells[rowIndex, 2].Value = customer.FullName;
                    worksheet.Cells[rowIndex, 3].Value = customer.CustomerId;
                    worksheet.Cells[rowIndex, 4].Value = balance.CurrencyCode;
                    worksheet.Cells[rowIndex, 5].Value = balance.Balance;
                    worksheet.Cells[rowIndex, 5].Style.Numberformat.Format = balance.CurrencyCode == "IRR" ? "#,##0" : "#,##0.00";

                    StyleDataRow(worksheet.Cells[rowIndex, 1, rowIndex, 5]);

                    rowIndex++;
                    index++;
                }
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            return package.GetAsByteArray();
        }

        private void StyleHeaderCell(ExcelRange range, int fontSize, bool bold)
        {
            range.Style.Font.Size = fontSize;
            range.Style.Font.Bold = bold;
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        private void StyleHeaderRow(ExcelRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            
            foreach (var cell in range)
            {
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
        }

        private void StyleDataRow(ExcelRange range)
        {
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            
            foreach (var cell in range)
            {
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
        }

        private void StyleInfoCell(ExcelRange cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        private string GetTransactionTypeText(string? type)
        {
            return type switch
            {
                "Buy" => "خرید",
                "Sell" => "فروش",
                "Document" => "دریافت",
                "DocumentDebit" => "پرداخت",
                "InitialBalance" => "موجودی اولیه",
                "ManualAdjustment" => "تعدیل دستی",
                _ => "تراکنش"
            };
        }

        private void StyleTransactionTypeCell(ExcelRange cell, string? type)
        {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            
            switch (type)
            {
                case "Buy":
                    // Light green for Buy (خرید)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                    break;
                case "Sell":
                    // Light coral for Sell (فروش)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                    break;
                case "Document":
                    // Light blue for Document (دریافت)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                    break;
                case "DocumentDebit":
                    // Light yellow for DocumentDebit (پرداخت)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    break;
                case "InitialBalance":
                    // Light cyan for InitialBalance (موجودی اولیه)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
                    break;
                case "ManualAdjustment":
                    // Light pink for ManualAdjustment (تعدیل دستی)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightPink);
                    break;
                default:
                    // Light gray for other transaction types
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    break;
            }
            
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }
    }
}