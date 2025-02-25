using Trading.Application.Telegram.Logging;

namespace Trading.API.Extensions;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddTelegramLogger(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, TelegramLoggerProvider>();
        return builder;
    }
}
