// using Microsoft.Extensions.Configuration;
// using ForexExchange.Services.Notifications.Helpers;

// namespace ForexExchange.Tests
// {
//     public class TelegramSettingsTests
//     {
//         [Fact]
//         public void IsAllowedChat_OnlyMatchesConfiguredTargetChatIds()
//         {
//             var configuration = new ConfigurationBuilder()
//                 .AddInMemoryCollection(
//                     new Dictionary<string, string?>
//                     {
//                         ["Notifications:Telegram:TargetChatIds:0"] = "111",
//                         ["Notifications:Telegram:TargetChatIds:1"] = " 222 ",
//                     }
//                 )
//                 .Build();

//             Assert.True(TelegramSettings.IsAllowedChat(configuration, "111"));
//             Assert.True(TelegramSettings.IsAllowedChat(configuration, "222"));
//             Assert.False(TelegramSettings.IsAllowedChat(configuration, "333"));
//             Assert.False(TelegramSettings.IsAllowedChat(configuration, null));
//         }

//         [Fact]
//         public void IsValidApiToken_RejectsMissingOrWrongToken()
//         {
//             var configuration = new ConfigurationBuilder()
//                 .AddInMemoryCollection(
//                     new Dictionary<string, string?>
//                     {
//                         ["Notifications:Telegram:Commands:ApiToken"] = "secret-token",
//                     }
//                 )
//                 .Build();

//             Assert.True(TelegramSettings.IsValidApiToken(configuration, "secret-token"));
//             Assert.False(TelegramSettings.IsValidApiToken(configuration, "wrong"));
//             Assert.False(TelegramSettings.IsValidApiToken(configuration, null));
//         [Theory]
//         [InlineData("/rates", "rates")]
//         [InlineData("/rates@TabanExcahnge_bot", "rates")]
//         [InlineData("/HELP", "help")]
//         [InlineData("hello", null)]
//         public void ParseCommand_ReadsSlashCommands(string? text, string? expected)
//         {
//             Assert.Equal(expected, TelegramSettings.ParseCommand(text));
//         }
//     }
// }}
