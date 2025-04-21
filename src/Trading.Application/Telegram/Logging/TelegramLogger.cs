using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Trading.Common.Extensions;
using Trading.Common.Models;

namespace Trading.Application.Telegram.Logging;

public class TelegramLogger : ILogger
{
    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;
    private readonly ITelegramBotClient _botClient;
    private readonly string _categoryName;
    private readonly string _chatId;

    public TelegramLogger(ITelegramBotClient botClient,
                          IOptions<TelegramLoggerOptions> loggerOptions,
                          TelegramSettings settings,
                          string categoryName)
    {
        _botClient = botClient;
        _loggerOptions = loggerOptions;
        _categoryName = categoryName;
        _chatId = settings.ChatId ?? throw new ArgumentNullException(nameof(settings), "TelegramSettings is not valid.");
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None
            && logLevel >= _loggerOptions.Value.MinimumLevel
            && !_loggerOptions.Value.ExcludeCategories.Contains(_categoryName);
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // 将异步操作包装在一个可等待的任务中
        var task = LogInternalAsync(logLevel, state, exception, formatter);
        task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    // 新增内部异步方法
    internal async Task LogInternalAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = new StringBuilder();
            message.AppendLine($"<b>{GetEmoji(logLevel)} {logLevel.ToString()}</b> ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})");
            if (_loggerOptions.Value.IncludeCategory)
            {
                message.AppendLine($"📁 {_categoryName}");
            }
            message.AppendLine($"<pre>{formatter(state, exception).ToTelegramSafeString()}");

            if (exception != null)
            {
                message.AppendLine($"{exception.Message.ToTelegramSafeString()}</pre>");
                message.AppendLine($"🔍 {exception.StackTrace?.ToTelegramSafeString()}");
            }
            else
            {
                message.AppendLine("</pre>");
            }

            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = message.ToString(),
                ParseMode = ParseMode.Html,
            }
            );
        }
        catch (Exception ex)
        {
            try
            {
                await _botClient.SendRequest(new SendMessageRequest
                {
                    ChatId = _chatId,
                    Text = ex.Message,
                    ParseMode = ParseMode.Html,
                });
            }
            catch
            {
                // fallback
            }
        }
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
