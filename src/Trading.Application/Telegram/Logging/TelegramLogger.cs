using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Common.Extensions;
using Trading.Common.Models;

namespace Trading.Application.Telegram.Logging;

internal sealed class DisposableScope : IDisposable
{
    private readonly Action _onDispose;

    public DisposableScope(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        _onDispose();
    }
}

public class TelegramLoggerScope
{
    public string? Title { get; set; }
    public bool DisableNotification { get; set; } = true;
    public ParseMode ParseMode { get; set; } = ParseMode.Html;
    public ReplyMarkup? ReplyMarkup { get; set; }
}

public class TelegramLogger : ILogger
{
    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;
    private readonly ITelegramBotClient _botClient;
    private readonly string _categoryName;
    private readonly string _chatId;
    private readonly AsyncLocal<Stack<TelegramLoggerScope>> _scopeStack = new();

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

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        var scopeStack = _scopeStack.Value ??= new Stack<TelegramLoggerScope>();

        if (state is not TelegramLoggerScope scope)
        {
            // If the state is not a TelegramLoggerScope, we create a new one
            scope = new TelegramLoggerScope();
        }

        scopeStack.Push(scope);

        return new DisposableScope(() =>
        {
            if (_scopeStack.Value?.Count > 0)
            {
                _scopeStack.Value.Pop();
            }
        });
    }

    private TelegramLoggerScope GetCurrentScope()
    {
        var scopeStack = _scopeStack.Value;
        return scopeStack?.Count > 0
            ? scopeStack.Peek()
            : new TelegramLoggerScope();
    }

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

        var task = LogInternalAsync(logLevel, state, exception, formatter);
        task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task LogInternalAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var scope = GetCurrentScope();

            var text = scope.ParseMode == ParseMode.Html
                ? BuildHtmlMessage(logLevel, state, exception, formatter, scope)
                : formatter(state, exception);

            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = text,
                ParseMode = scope.ParseMode,
                DisableNotification = scope.DisableNotification,
                ReplyMarkup = scope.ReplyMarkup
            });
        }
        catch (Exception ex)
        {
            try
            {
                await _botClient.SendRequest(new SendMessageRequest
                {
                    ChatId = _chatId,
                    Text = $"Failed to send log message: {ex.Message}",
                    ParseMode = ParseMode.Html,
                });
            }
            catch
            {
                // fallback
            }
        }
    }

    private string BuildHtmlMessage<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, TelegramLoggerScope scope)
    {
        var message = new StringBuilder();

        var title = !string.IsNullOrEmpty(scope.Title)
            ? scope.Title
            : $"{GetEmoji(logLevel)} {logLevel}";

        message.AppendLine($"<b>{title}</b> ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})");

        if (_loggerOptions.Value.IncludeCategory)
        {
            message.AppendLine($"📁 {_categoryName}");
        }

        if (exception != null)
        {
            message.AppendLine("<pre>");
            message.AppendLine(exception.Message.ToTelegramSafeString());
            message.AppendLine(formatter(state, exception).ToTelegramSafeString());
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                message.AppendLine(exception.StackTrace.ToTelegramSafeString());
            }
            message.AppendLine("</pre>");
        }
        else
        {
            message.AppendLine($"<pre>{formatter(state, exception).ToTelegramSafeString()}</pre>");
        }

        return message.ToString();
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
