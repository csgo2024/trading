using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Application.Telegram.Logging;

namespace Trading.Application.Telegram;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddTelegramLogger(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, TelegramLoggerProvider>();
        return builder;
    }

    public static ILoggingBuilder AddTelegramLogger(this ILoggingBuilder builder,
                                                    Action<TelegramLoggerOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddSingleton<ILoggerProvider, TelegramLoggerProvider>();
        return builder;
    }

    public static ILoggingBuilder AddTelegramLogger(this ILoggingBuilder builder,
                                                    IConfiguration configuration)
    {
        builder.Services.Configure<TelegramLoggerOptions>(configuration.GetSection("Logging:TelegramLogger"));
        builder.Services.AddSingleton<ILoggerProvider, TelegramLoggerProvider>();
        return builder;
    }
}
