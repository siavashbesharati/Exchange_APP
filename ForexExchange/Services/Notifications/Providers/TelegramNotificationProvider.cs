using System.Text;
using System.Text.Json;


namespace ForexExchange.Services.Notifications.Providers
{
    /// <summary>
    /// Telegram Bot notification provider template
    /// قالب ارائه‌دهنده اعلان ربات تلگرام
    /// </summary>
    public class TelegramNotificationProvider : INotificationProvider
    {
        private readonly ILogger<TelegramNotificationProvider> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        private readonly string _proxyBaseUrl;
        private readonly string _botToken;
        private readonly string _targetChatId;

        public string ProviderName => "Telegram";

        public bool IsEnabled =>
            _configuration.GetValue<bool>("Notifications:Telegram:Enabled", true);

        public TelegramNotificationProvider(
            ILogger<TelegramNotificationProvider> logger,
            IConfiguration configuration,
            HttpClient httpClient
        )
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;

            _proxyBaseUrl =
                _configuration["Telegram:ProxyBaseUrl"] ?? "https://reverse.darkgpt.workers.dev";
            _botToken =
                _configuration["Telegram:BotToken"]
                ?? "8505299642:AAHtKoI12ykna0VI2F7q-29gsjjyKvUCzAI";
            _targetChatId = _configuration["Telegram:TargetChatId"] ?? "7556753514";
            _logger.LogInformation(
                "Telegram Notification Provider initialized. ProxyBaseUrl: {ProxyBaseUrl}, BotToken: {BotToken}, TargetChatId: {TargetChatId}",
                _proxyBaseUrl,
                _botToken,
                _targetChatId
            );
        }

        private async Task SendTelegramMessageInternalAsync(string title, string message)
        {
            var messageToSend = $"**{title}**\n\n{message}";

            // ساخت آدرس نهایی متصل به ورکر کلودفلر
            var url = $"{_proxyBaseUrl}/bot{_botToken}/sendMessage";

            // ساخت Payload تلگرام با چت‌آیدی هاردکد شده
            var payload = new
            {
                chat_id = _targetChatId,
                text = messageToSend,
                parse_mode = "Markdown",
            };

            _logger.LogDebug("Serializing Telegram payload.");
            var json = JsonSerializer.Serialize(payload);
            _logger.LogDebug("Telegram payload serialized. JSON: {Json}", json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation(
                    "Sending Telegram message to chat ID {ChatId}. URL: {Url}",
                    _targetChatId,
                    url
                );
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Telegram message sent successfully. Response: {Response}",
                        responseContent
                    );
                }
                else
                {
                    _logger.LogError(
                        "Failed to send Telegram message. Status Code: {StatusCode}, Response: {Response}",
                        response.StatusCode,
                        responseContent
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Telegram message");
            }
        }

        public async Task SendOrderNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation("try to send Order notifcation  isEnabled:", IsEnabled);

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context.Title, context.Message);
        }

        public async Task SendAccountingDocumentNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "try to send AccountingDocument notifcation  isEnabled:",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context.Title, context.Message);
        }

        public async Task SendCustomerNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation("try to send Customer notifcation  isEnabled:", IsEnabled);

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context.Title, context.Message);
        }

        public async Task SendSystemNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation("try to send System notifcation  isEnabled:", IsEnabled);

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context.Title, context.Message);
        }

        public async Task SendManualAdjustmentNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "try to send ManualAdjustment notifcation  isEnabled:",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context.Title, context.Message);
        }
    }
}
