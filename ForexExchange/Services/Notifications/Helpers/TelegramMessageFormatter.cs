using System.Net;
using System.Text;
using ForexExchange.Helpers;

namespace ForexExchange.Services.Notifications.Helpers
{
    public static class TelegramMessageFormatter
    {
        private const string Divider = "━━━━━━━━━━━━━━━━━━";

        public static string Format(NotificationContext context)
        {
            var body = context.EventType switch
            {
                NotificationEventType.OrderCreated => FormatOrderCreated(context),
                NotificationEventType.OrderDeleted => FormatOrderDeleted(context),
                NotificationEventType.AccountingDocumentCreated => FormatAccountingDocumentCreated(context),
                NotificationEventType.AccountingDocumentVerified => FormatAccountingDocumentVerified(context),
                NotificationEventType.AccountingDocumentDeleted => FormatAccountingDocumentCreated(context),
                NotificationEventType.CustomerRegistered => FormatCustomerRegistered(context),
                NotificationEventType.TaskAssignment
                    or NotificationEventType.TaskDueReminder
                    or NotificationEventType.TaskOverdue
                    or NotificationEventType.TaskProgress
                    or NotificationEventType.TaskCompleted => FormatTask(context),
                NotificationEventType.SystemError => FormatSystemError(context),
                NotificationEventType.SystemMaintenance => FormatSystemMaintenance(context),
                NotificationEventType.ManualAdjustment => FormatManualAdjustment(context),
                _ => FormatGeneric(context),
            };

            return body;
        }

        private static string FormatOrderCreated(NotificationContext context)
        {
            var data = context.Data;
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📋 شماره معامله", $"#{GetValue(data, "orderId")}");
            AppendLine(sb, "👤 مشتری", GetValue(data, "customerName"));
            AppendLine(
                sb,
                "💱 مبلغ",
                $"{GetValue(data, "amount")} {GetValue(data, "fromCurrency")} → {GetValue(data, "totalAmount")} {GetValue(data, "toCurrency")}"
            );
            AppendLine(sb, "📈 نرخ", GetValue(data, "rate"));
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatOrderDeleted(NotificationContext context)
        {
            var data = context.Data;
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📋 شماره معامله", $"#{GetValue(data, "orderId")}");
            AppendLine(sb, "👤 مشتری", GetValue(data, "customerName"));
            AppendLine(
                sb,
                "💱 مبلغ",
                $"{GetValue(data, "amount")} {GetValue(data, "fromCurrency")} → {GetValue(data, "totalAmount")} {GetValue(data, "toCurrency")}"
            );
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatAccountingDocumentCreated(NotificationContext context)
        {
            var data = context.Data;
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📄 شماره سند", $"#{GetValue(data, "documentId")}");
            AppendLine(sb, "📝 عنوان", GetValue(data, "title"));
            AppendLine(sb, "💰 مبلغ", $"{GetValue(data, "amount")} {GetValue(data, "currencyCode")}");
            AppendPartyLines(sb, data);
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatAccountingDocumentVerified(NotificationContext context)
        {
            var data = context.Data;
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📄 شماره سند", $"#{GetValue(data, "documentId")}");
            AppendLine(sb, "📝 عنوان", GetValue(data, "title"));
            AppendLine(sb, "💰 مبلغ", $"{GetValue(data, "amount")} {GetValue(data, "currencyCode")}");
            AppendPartyLines(sb, data);
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatManualAdjustment(NotificationContext context)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);

            sb.AppendLine("📝 توضیحات:");
            foreach (var line in context.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                sb.AppendLine($"  • <b>{Escape(line)}</b>");
            }

            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatCustomerRegistered(NotificationContext context)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "👤 مشتری", GetValue(context.Data, "fullName"));
            AppendLine(sb, "📱 تلفن", GetValue(context.Data, "phoneNumber"));
            AppendLine(sb, "🆔 شناسه", $"#{GetValue(context.Data, "customerId")}");
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatTask(NotificationContext context)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📋 وظیفه", GetValue(context.Data, "title"));
            AppendLine(sb, "🆔 شناسه", $"#{GetValue(context.Data, "taskId")}");
            AppendLine(sb, "👤 مسئول", GetValue(context.Data, "assignedTo"));
            AppendLine(sb, "📅 سررسید", GetValue(context.Data, "dueDate"));
            AppendLine(sb, "📊 وضعیت", GetValue(context.Data, "status"));

            var description = GetValue(context.Data, "description");
            if (description != "—" && !string.IsNullOrWhiteSpace(description))
                AppendLine(sb, "📝 توضیحات", description);

            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatSystemError(NotificationContext context)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📝 خطا", context.Message);
            AppendOptionalLine(sb, "📍 مسیر", context.Data, "path");
            AppendOptionalLine(sb, "⚙️ نوع خطا", context.Data, "exceptionType");
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatSystemMaintenance(NotificationContext context)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "🛠️ عملیات", context.Message);
            AppendOptionalLine(sb, "📦 جزئیات", context.Data, "details");
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static string FormatGeneric(NotificationContext context)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, context.Title);
            AppendLine(sb, "📝 پیام", context.Message);
            AppendActorFooter(sb, context);
            return sb.ToString();
        }

        private static void AppendPartyLines(StringBuilder sb, Dictionary<string, object> data)
        {
            var payer = GetValue(data, "payerCustomerName");
            var receiver = GetValue(data, "receiverCustomerName");

            if (payer != "—")
                AppendLine(sb, "📤 پرداخت‌کننده", payer);
            if (receiver != "—")
                AppendLine(sb, "📥 دریافت‌کننده", receiver);
        }

        private static void AppendHeader(StringBuilder sb, string title)
        {
            sb.AppendLine($"<b>{Escape(title)}</b>");
            sb.AppendLine(Divider);
        }

        private static void AppendLine(StringBuilder sb, string label, string value)
        {
            sb.AppendLine($"{label}: <b>{Escape(value)}</b>");
        }

        private static void AppendOptionalLine(
            StringBuilder sb,
            string label,
            Dictionary<string, object> data,
            string key
        )
        {
            var value = GetValue(data, key);
            if (value != "—" && !string.IsNullOrWhiteSpace(value))
                AppendLine(sb, label, value);
        }

        private static void AppendActorFooter(StringBuilder sb, NotificationContext context)
        {
            var actor = context.Actor;
            sb.AppendLine();
            sb.AppendLine(Divider);
            AppendLine(sb, "👨‍💼 کاربر", actor.UserName);
            AppendLine(sb, "🌐 IP", string.IsNullOrWhiteSpace(actor.IpAddress) ? "—" : actor.IpAddress);
            AppendLine(sb, "🖥️ مرورگر", actor.Browser);
            AppendLine(sb, "💻 سیستم‌عامل", actor.OperatingSystem);
            AppendLine(sb, "🕐 زمان", context.OccurredAt.ToDisplayDateTime());
        }

        private static string GetValue(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return "—";

            return key switch
            {
                "amount" or "totalAmount" when value is IFormattable formattable =>
                    formattable.ToString("N0", null) ?? "—",
                "rate" when value is IFormattable formattable =>
                    formattable.ToString("N2", null) ?? "—",
                _ => value.ToString() ?? "—",
            };
        }

        private static string Escape(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "—");
        }
    }
}
