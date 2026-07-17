using System.Text;
using System.Text.Json;
using ForexExchange.Services.Notifications.Helpers;


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
        private readonly IReadOnlyList<string> _targetChatIds;

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
                _configuration["Notifications:Telegram:ProxyBaseUrl"]
                ?? "https://reverse.darkgpt.workers.dev";
            _botToken =
                _configuration["Notifications:Telegram:BotToken"]
                ?? "8377558116:AAEt1-NpbciCLJSecxmmF0zvqHmHUaYrsaQ";
            _targetChatIds = LoadTargetChatIds(_configuration);
            _logger.LogInformation(
                "Telegram Notification Provider initialized. ProxyBaseUrl: {ProxyBaseUrl}, TargetChatIds: {TargetChatIds}",
                _proxyBaseUrl,
                string.Join(", ", _targetChatIds)
            );
        }

        private static IReadOnlyList<string> LoadTargetChatIds(IConfiguration configuration)
        {
            var chatIds =
                configuration
                    .GetSection("Notifications:Telegram:TargetChatIds")
                    .Get<string[]>()
                ?? Array.Empty<string>();

            return chatIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private async Task SendTelegramMessageInternalAsync(NotificationContext context)
        {
            if (_targetChatIds.Count == 0)
            {
                _logger.LogWarning(
                    "No Telegram target chat IDs configured (Notifications:Telegram:TargetChatIds). Skipping message."
                );
                return;
            }

            var messageToSend = TelegramMessageFormatter.Format(context);
            var url = $"{_proxyBaseUrl}/bot{_botToken}/sendMessage";

            var sendTasks = _targetChatIds.Select(chatId =>
                SendTelegramMessageToChatAsync(url, chatId, messageToSend)
            );
            await Task.WhenAll(sendTasks);
        }

        private async Task SendTelegramMessageToChatAsync(
            string url,
            string chatId,
            string messageToSend
        )
        {
            var payload = new
            {
                chat_id = chatId,
                text = messageToSend,
                parse_mode = "HTML",
            };

            _logger.LogDebug("Serializing Telegram payload for chat ID {ChatId}.", chatId);
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation(
                    "Sending Telegram message to chat ID {ChatId}. URL: {Url}",
                    chatId,
                    url
                );
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Telegram message sent successfully to chat ID {ChatId}. Response: {Response}",
                        chatId,
                        responseContent
                    );
                }
                else
                {
                    _logger.LogError(
                        "Failed to send Telegram message to chat ID {ChatId}. Status Code: {StatusCode}, Response: {Response}",
                        chatId,
                        response.StatusCode,
                        responseContent
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Telegram message to chat ID {ChatId}", chatId);
            }
        }

        public async Task SendOrderNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "Sending order Telegram notification. Enabled: {IsEnabled}",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context);
        }

        public async Task SendAccountingDocumentNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "Sending accounting document Telegram notification. Enabled: {IsEnabled}",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context);
        }

        public async Task SendCustomerNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "Sending customer Telegram notification. Enabled: {IsEnabled}",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context);
        }

        public async Task SendSystemNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "Sending system Telegram notification. Enabled: {IsEnabled}",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context);
        }

        public async Task SendManualAdjustmentNotificationAsync(NotificationContext context)
        {
            _logger.LogInformation(
                "Sending manual adjustment Telegram notification. Enabled: {IsEnabled}",
                IsEnabled
            );

            if (!IsEnabled)
                return;
            await SendTelegramMessageInternalAsync(context);
        }
    }
}
