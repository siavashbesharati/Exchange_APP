using System.Security.Cryptography;
using System.Text;

namespace ForexExchange.Services.Notifications.Helpers
{
    /// <summary>
    /// Shared Telegram notification / command settings.
    /// Chat allowlist is the same TargetChatIds used for outbound alerts.
    /// </summary>
    public static class TelegramSettings
    {
        public const string ApiTokenHeaderName = "X-Telegram-Api-Token";

        public static IReadOnlyList<string> GetTargetChatIds(IConfiguration configuration)
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

        public static string GetCommandsApiToken(IConfiguration configuration)
        {
            return configuration["Notifications:Telegram:Commands:ApiToken"]?.Trim()
                ?? string.Empty;
        }

        public static bool IsPollingEnabled(IConfiguration configuration)
        {
            if (configuration.GetValue<bool?>("Notifications:Telegram:Enabled") != true)
                return false;

            // Default on so /rates works locally and on Plesk without Serverless.
            return configuration.GetValue<bool?>("Notifications:Telegram:Commands:PollingEnabled")
                != false;
        }

        public static bool IsAllowedChat(IConfiguration configuration, string? chatId)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return false;

            var allowed = GetTargetChatIds(configuration);
            var normalized = chatId.Trim();
            return allowed.Contains(normalized, StringComparer.Ordinal);
        }

        public static string? ParseCommand(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var token = text.Trim().Split(' ', 2)[0];
            if (!token.StartsWith('/'))
                return null;

            var slash = token[1..];
            var at = slash.IndexOf('@');
            if (at >= 0)
                slash = slash[..at];

            return string.IsNullOrWhiteSpace(slash) ? null : slash.ToLowerInvariant();
        }

        public static bool IsValidApiToken(IConfiguration configuration, string? providedToken)
        {
            var expected = GetCommandsApiToken(configuration);
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(providedToken))
                return false;

            var providedBytes = Encoding.UTF8.GetBytes(providedToken);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);

            if (providedBytes.Length != expectedBytes.Length)
            {
                CryptographicOperations.FixedTimeEquals(expectedBytes, expectedBytes);
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}
