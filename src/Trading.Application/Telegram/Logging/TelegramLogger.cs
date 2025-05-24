using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
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

public class TelegramLogger : ILogger
{
    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;
    private readonly ITelegramBotClient _botClient;
    private readonly string _categoryName;
    private readonly string _chatId;
    private const string NotificationKey = "DisableNotification";
    private readonly AsyncLocal<Stack<IReadOnlyDictionary<string, object>>> _scopeStack = new();

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
        var scopeStack = _scopeStack.Value ??= new Stack<IReadOnlyDictionary<string, object>>();

        var scopeData = state as IReadOnlyDictionary<string, object>
            ?? new Dictionary<string, object> { { "Scope", state } };

        scopeStack.Push(scopeData);

        return new DisposableScope(() =>
        {
            if (_scopeStack.Value?.Count > 0)
            {
                _scopeStack.Value.Pop();
            }
        });
    }

    private bool ShouldDisableNotification()
    {
        var scopeStack = _scopeStack.Value;
        if (scopeStack == null || scopeStack.Count == 0)
        {
            return true;
        }

        foreach (var scope in scopeStack)
        {
            if (scope.TryGetValue(NotificationKey, out var value) &&
                value is bool disableNotification)
            {
                return disableNotification;
            }
        }
        return true;
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

            if (exception != null)
            {
                message.AppendLine($"<pre>{exception.Message.ToTelegramSafeString()}");
                message.AppendLine($"{formatter(state, exception).ToTelegramSafeString()}");
                message.AppendLine($"{exception.StackTrace?.ToTelegramSafeString()}</pre>");
            }
            else
            {
                message.AppendLine($"<pre>{formatter(state, exception).ToTelegramSafeString()}</pre>");
            }

            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = message.ToString(),
                ParseMode = ParseMode.Html,
                DisableNotification = ShouldDisableNotification(),
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
