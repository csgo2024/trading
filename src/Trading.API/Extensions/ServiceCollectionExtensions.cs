using System.Text;
using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Trading.API.HostServices;
using Trading.API.Services.Trading.Account;
using Trading.API.Services.Trading.Executors;
using Trading.Application.Commands;
using Trading.Application.Middlerwares;
using Trading.Application.Queries;
using Trading.Application.Telegram;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.HostServices;
using Trading.Common.Models;

namespace Trading.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradingServices(this IServiceCollection services)
    {
        services.AddSingleton<FeatureProcessor>();
        services.AddSingleton<SpotProcessor>();
        services.AddSingleton<AccountProcessorFactory>();

        services.AddSingleton<BottomBuyExecutor>();
        services.AddSingleton<DCABuyExecutor>();
        services.AddSingleton<ExecutorFactory>();
        services.AddHostedService<TradingService>();
        return services;
    }

    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection("TelegramSettings"));
        services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<TelegramSettings>>().Value;
            return new TelegramBotClient(settings.BotToken ?? throw new InvalidOperationException("TelegramSettings is not valid."));
        });
        services.AddSingleton<HelpCommandHandler>();
        services.AddSingleton<StatusCommandHandler>();
        services.AddSingleton<CreateStrategyHandler>();
        services.AddSingleton<DeleteStrategyHandler>();
        services.AddSingleton<StopStrategyHandler>();
        services.AddSingleton<ResumeStrategyHandler>();
        services.AddSingleton<TelegramCommandHandlerFactory>();
        services.AddSingleton<ITelegramCommandHandler, TelegramCommandHandler>();

        services.AddSingleton<IErrorMessageResolver, DefaultErrorMessageResolver>();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddDebug();
            builder.AddTelegramLogger();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddHostedService<TelegramBotService>();
        return services;
    }
    public static IServiceCollection AddBinance(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provider =>
        {
            var privateKey = configuration.GetSection("PrivateKey")?.Value ?? string.Empty;
            var query = provider.GetRequiredService<ICredentialQuery>();
            var settings = query.GetCredential();
            var apiKey = "your-api-key";
            if (settings?.ApiKey != null)
            {
                apiKey = Encoding.UTF8.GetString(CreateCredentialCommandHandler.DecryptData(settings.ApiKey, privateKey));
            }
            var apiSecret = "your-secret";
            if (settings?.ApiSecret != null)
            {
                apiSecret = Encoding.UTF8.GetString(CreateCredentialCommandHandler.DecryptData(settings.ApiSecret, privateKey));
            }
            var restClient = new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            });
            return restClient;
        });

        services.AddSingleton<BinanceSpotRestClientWrapper>(provider =>
        {
            var binanceRestClient = provider.GetRequiredService<BinanceRestClient>();
            return new BinanceSpotRestClientWrapper(binanceRestClient.SpotApi.Trading,
                                                    binanceRestClient.SpotApi.ExchangeData);
        });
        services.AddSingleton<BinanceFeatureRestClientWrapper>(provider =>
        {
            var binanceRestClient = provider.GetRequiredService<BinanceRestClient>();
            return new BinanceFeatureRestClientWrapper(binanceRestClient.UsdFuturesApi.Trading,
                                                       binanceRestClient.UsdFuturesApi.ExchangeData);
        });
        return services;
    }
}
