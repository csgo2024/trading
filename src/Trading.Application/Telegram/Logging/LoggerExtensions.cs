using Microsoft.Extensions.Logging;

namespace Trading.Application.Telegram.Logging;

public static class LoggerExtensions
{
    public static void LogInformationWithAlert(this ILogger logger, string? message, params object?[] args)
    {
        using (logger.BeginScope(new TelegramLoggerScope { DisableNotification = false }))
        {
            logger.LogInformation(message, args);
        }
    }
    public static void LogErrorWithAlert(this ILogger logger, string? message, params object?[] args)
    {
        using (logger.BeginScope(new TelegramLoggerScope { DisableNotification = false }))
        {
            logger.Log(LogLevel.Error, message, args);
        }
    }
    public static void LogErrorWithAlert(this ILogger logger, Exception? exception, string? message, params object?[] args)
    {
        using (logger.BeginScope(new TelegramLoggerScope { DisableNotification = false }))
        {
            logger.Log(LogLevel.Error, exception, message, args);
        }
    }
}
