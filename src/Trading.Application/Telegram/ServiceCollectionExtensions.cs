using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.HostServices;
using Trading.Common.Models;

namespace Trading.Application.Telegram;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Telegram bot services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection("TelegramSettings"));
        services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<TelegramSettings>>().Value;
            return new TelegramBotClient(settings.BotToken ?? throw new InvalidOperationException("TelegramSettings is not valid."));
        });
        services.AddSingleton<HelpCommandHandler>();
        services.AddSingleton<StrategyCommandHandler>();
        services.AddSingleton<AlertCommandHandler>();
        services.AddSingleton<TelegramCommandHandlerFactory>();
        services.AddSingleton<ITelegramCommandHandler, TelegramCommandHandler>();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
            builder.AddTelegramLogger(configuration);
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddHostedService<TelegramBotService>();
        return services;
    }
}
