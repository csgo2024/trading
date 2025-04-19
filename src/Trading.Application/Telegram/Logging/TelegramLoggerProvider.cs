using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Trading.Common.Models;

namespace Trading.Application.Telegram.Logging;

public class TelegramLoggerProvider : ILoggerProvider
{
    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramSettings _settings;

    public TelegramLoggerProvider(ITelegramBotClient botClient,
                                  IOptions<TelegramLoggerOptions> loggerOptions,
                                  IOptions<TelegramSettings> settings)
    {
        _botClient = botClient;
        _loggerOptions = loggerOptions;
        _settings = settings.Value;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TelegramLogger(_botClient, _loggerOptions, _settings, categoryName);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
