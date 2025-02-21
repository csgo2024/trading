using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Trading.Common.Models;

namespace Trading.API.Application.Logging
{
    public class TelegramLogger : ILogger
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _categoryName;
        private readonly string _chatId;

        public TelegramLogger(ITelegramBotClient botClient, TelegramSettings settings, string categoryName)
        {
            _botClient = botClient;
            _categoryName = categoryName;
            _chatId = settings.ChatId;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            Task.Run(async () =>
            {
                try
                {
                    var message = new StringBuilder();
                    message.AppendLine($"{GetEmoji(logLevel)} [{logLevel.ToString()}]");
                    message.AppendLine($"⏰ {DateTime.UtcNow.AddHours(8)}");
                    message.AppendLine($"{formatter(state, exception)}");

                    if (exception != null)
                    {
                        message.AppendLine($"❌ {exception.Message}");
                        message.AppendLine($"🔍 {exception.StackTrace}");
                    }

                    await _botClient.SendTextMessageAsync(
                        chatId: _chatId,
                        text: message.ToString(),
                        parseMode: ParseMode.Html
                    );
                }
                catch
                {
                    // Fallback logging if needed
                }
            });
        }

        private static string GetEmoji(LogLevel level) => level switch
        {
            LogLevel.Trace => "🔍",
            LogLevel.Debug => "🔧",
            LogLevel.Information => "ℹ️",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            LogLevel.Critical => "🆘",
            _ => "📝"
        };
    }
}