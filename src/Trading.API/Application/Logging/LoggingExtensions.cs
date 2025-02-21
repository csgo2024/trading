
namespace Trading.API.Application.Logging
{
    public static class LoggingExtensions
    {
        public static ILoggingBuilder AddTelegramLogger(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, TelegramLoggerProvider>();
            return builder;
        }
    }
}