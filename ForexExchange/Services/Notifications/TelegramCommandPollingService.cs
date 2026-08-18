using System.Net.Http.Json;
using System.Text.Json;
using ForexExchange.Services.Notifications.Helpers;

namespace ForexExchange.Services.Notifications
{
    /// <summary>
    /// Pulls bot commands through the same Telegram proxy used for notifications.
    /// This is what makes /rates work on localhost and Plesk without Serverless/webhooks.
    /// </summary>
    public class TelegramCommandPollingService : BackgroundService
    {
        private static readonly HashSet<string> HandledCommands = new(StringComparer.Ordinal)
        {
            "rates",
            "start",
            "help",
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramCommandPollingService> _logger;

        public TelegramCommandPollingService(
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<TelegramCommandPollingService> logger
        )
        {
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!TelegramSettings.IsPollingEnabled(_configuration))
            {
                _logger.LogInformation("Telegram command polling is disabled.");
                return;
            }

            var proxyBaseUrl = _configuration["Notifications:Telegram:ProxyBaseUrl"]?.Trim();
            var botToken = _configuration["Notifications:Telegram:BotToken"]?.Trim();
            if (string.IsNullOrWhiteSpace(proxyBaseUrl) || string.IsNullOrWhiteSpace(botToken))
            {
                _logger.LogWarning(
                    "Telegram command polling skipped: ProxyBaseUrl or BotToken is missing."
                );
                return;
            }

            var apiRoot = $"{proxyBaseUrl.TrimEnd('/')}/bot{botToken}";
            var client = _httpClientFactory.CreateClient("TelegramCommands");
            var offset = 0L;

            try
            {
                using var webhookResponse = await PostAsync(
                    client,
                    $"{apiRoot}/deleteWebhook",
                    new { drop_pending_updates = true },
                    stoppingToken
                );
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not clear Telegram webhook before polling.");
            }

            _logger.LogInformation(
                "Telegram command polling started. Allowed chats: {ChatIds}",
                string.Join(", ", TelegramSettings.GetTargetChatIds(_configuration))
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var document = await PostAsync(
                        client,
                        $"{apiRoot}/getUpdates",
                        new
                        {
                            offset,
                            timeout = 25,
                            allowed_updates = new[] { "message" },
                        },
                        stoppingToken
                    );

                    if (document == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                        continue;
                    }

                    var root = document.RootElement;
                    if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                    {
                        _logger.LogWarning("Telegram getUpdates failed: {Body}", root.GetRawText());
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    foreach (var update in root.GetProperty("result").EnumerateArray())
                    {
                        var updateId = update.GetProperty("update_id").GetInt64();
                        offset = Math.Max(offset, updateId + 1);
                        await HandleUpdateAsync(client, apiRoot, update, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telegram command polling loop failed.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task HandleUpdateAsync(
            HttpClient client,
            string apiRoot,
            JsonElement update,
            CancellationToken cancellationToken
        )
        {
            if (!update.TryGetProperty("message", out var message))
                return;

            var text = message.TryGetProperty("text", out var textElement)
                ? textElement.GetString()
                : null;
            var command = TelegramSettings.ParseCommand(text);
            if (command == null || !HandledCommands.Contains(command))
                return;

            var chatId = ReadId(message, "chat");
            var fromId = ReadId(message, "from");
            var fromName = ReadUsername(message);

            _logger.LogInformation(
                "Telegram command /{Command} from chat {ChatId} user {FromId} ({FromName})",
                command,
                chatId,
                fromId,
                fromName
            );

            if (!TelegramSettings.IsAllowedChat(_configuration, chatId)
                && !TelegramSettings.IsAllowedChat(_configuration, fromId))
            {
                _logger.LogWarning(
                    "Rejected Telegram command from unauthorized chat {ChatId} user {FromId} ({FromName}).",
                    chatId,
                    fromId,
                    fromName
                );

                if (!string.IsNullOrWhiteSpace(chatId))
                {
                    await SendHtmlAsync(
                        client,
                        apiRoot,
                        chatId,
                        "شما مجاز به استفاده از این ربات نیستید.",
                        cancellationToken
                    );
                }

                return;
            }

            IReadOnlyList<string> replies;
            using (var scope = _scopeFactory.CreateScope())
            {
                var commandService =
                    scope.ServiceProvider.GetRequiredService<TelegramBotCommandService>();
                replies = command switch
                {
                    "rates" => await commandService.BuildRatesMessagesAsync(cancellationToken),
                    _ => new[] { TelegramRatesMessageFormatter.FormatHelp() },
                };
            }

            foreach (var reply in replies)
            {
                await SendHtmlAsync(client, apiRoot, chatId!, reply, cancellationToken);
            }
        }

        private async Task SendHtmlAsync(
            HttpClient client,
            string apiRoot,
            string chatId,
            string text,
            CancellationToken cancellationToken
        )
        {
            try
            {
                using var response = await PostAsync(
                    client,
                    $"{apiRoot}/sendMessage",
                    new
                    {
                        chat_id = chatId,
                        text,
                        parse_mode = "HTML",
                    },
                    cancellationToken
                );

                if (response == null)
                    return;

                if (!response.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                {
                    _logger.LogError(
                        "Failed to send Telegram command reply to {ChatId}: {Body}",
                        chatId,
                        response.RootElement.GetRawText()
                    );
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error sending Telegram command reply to {ChatId}", chatId);
            }
        }

        private static async Task<JsonDocument?> PostAsync(
            HttpClient client,
            string url,
            object payload,
            CancellationToken cancellationToken
        )
        {
            using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }

        private static string? ReadId(JsonElement message, string objectName)
        {
            if (!message.TryGetProperty(objectName, out var obj)
                || !obj.TryGetProperty("id", out var id))
            {
                return null;
            }

            return id.ValueKind switch
            {
                JsonValueKind.Number => id.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonValueKind.String => id.GetString(),
                _ => null,
            };
        }

        private static string ReadUsername(JsonElement message)
        {
            if (!message.TryGetProperty("from", out var from))
                return "unknown";

            if (from.TryGetProperty("username", out var username)
                && username.ValueKind == JsonValueKind.String)
            {
                return "@" + username.GetString();
            }

            var first = from.TryGetProperty("first_name", out var firstName)
                ? firstName.GetString()
                : null;
            return string.IsNullOrWhiteSpace(first) ? "unknown" : first!;
        }
    }
}
