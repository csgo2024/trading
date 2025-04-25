using Microsoft.Extensions.Logging;

public static class LoggerExtensions
{
    private const string NotificationKey = "DisableNotification";

    public static IDisposable BeginSilentScope(this ILogger logger)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [NotificationKey] = true
        })!;
    }

    public static void LogInformationWithAlert(this ILogger logger, string? message, params object?[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object> { [NotificationKey] = false }))
        {
            logger.LogInformation(message, args);
        }
    }
    public static void LogErrorWithAlert(this ILogger logger, string? message, params object?[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object> { [NotificationKey] = false }))
        {
            logger.Log(LogLevel.Error, message, args);
        }
    }
    public static void LogErrorWithAlert(this ILogger logger, Exception? exception, string? message, params object?[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object> { [NotificationKey] = false }))
        {
            logger.Log(LogLevel.Error, exception, message, args);
        }
    }
}
