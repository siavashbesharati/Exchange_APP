using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForexExchange.Services.Notifications;
using ForexExchange.Services.Notifications.Helpers;

namespace ForexExchange.Controllers
{
    public class TelegramBotCommandRequest
    {
        public string? ChatId { get; set; }
        public string? Command { get; set; }
    }

    /// <summary>
    /// Token-protected command API used by the Telegram Serverless handler.
    /// Only chats in Notifications:Telegram:TargetChatIds are allowed.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/telegram")]
    public class TelegramBotController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly TelegramBotCommandService _commandService;
        private readonly ILogger<TelegramBotController> _logger;

        public TelegramBotController(
            IConfiguration configuration,
            TelegramBotCommandService commandService,
            ILogger<TelegramBotController> logger
        )
        {
            _configuration = configuration;
            _commandService = commandService;
            _logger = logger;
        }

        [HttpPost("command")]
        public async Task<IActionResult> Command(
            [FromBody] TelegramBotCommandRequest? request,
            CancellationToken cancellationToken
        )
        {
            var providedToken = Request.Headers[TelegramSettings.ApiTokenHeaderName].ToString();
            if (!TelegramSettings.IsValidApiToken(_configuration, providedToken))
            {
                _logger.LogWarning("Rejected Telegram command: invalid API token.");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request == null || !TelegramSettings.IsAllowedChat(_configuration, request.ChatId))
            {
                _logger.LogWarning(
                    "Rejected Telegram command from unauthorized chat {ChatId}.",
                    request?.ChatId
                );
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var command = NormalizeCommand(request.Command);
            IReadOnlyList<string> messages = command switch
            {
                "rates" => await _commandService.BuildRatesMessagesAsync(cancellationToken),
                "start" or "help" => new[] { TelegramRatesMessageFormatter.FormatHelp() },
                _ => Array.Empty<string>(),
            };

            return Ok(new { messages });
        }

        private static string NormalizeCommand(string? command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return string.Empty;

            var token = command.Trim().Split(' ', 2)[0];
            var slash = token.StartsWith('/') ? token[1..] : token;
            var at = slash.IndexOf('@');
            if (at >= 0)
                slash = slash[..at];

            return slash.ToLowerInvariant();
        }
    }
}
