using Microsoft.Extensions.Logging;

namespace Trading.Application.Telegram.Logging;

public readonly struct NotificationScope : IDisposable
{
    private static readonly AsyncLocal<bool?> _disableNotification = new();

    public NotificationScope(bool disableNotification)
    {
        _disableNotification.Value = disableNotification;
    }

    public static bool Current => _disableNotification.Value ?? true;

    public void Dispose() => _disableNotification.Value = null;
}

public static class LoggerExtensions
{
    public static void LogInformationWithAlert(this ILogger logger, string? message, params object?[] args)
    {
        using (new NotificationScope(false))
        {
            logger.Log(LogLevel.Information, message, args);
        }
    }
    public static void LogErrorWithAlert(this ILogger logger, string? message, params object?[] args)
    {
        using (new NotificationScope(false))
        {
            logger.Log(LogLevel.Error, message, args);
        }
    }
    public static void LogErrorWithAlert(this ILogger logger, Exception? exception, string? message, params object?[] args)
    {
        using (new NotificationScope(false))
        {
            logger.Log(LogLevel.Error, exception, message, args);
        }
    }
}
