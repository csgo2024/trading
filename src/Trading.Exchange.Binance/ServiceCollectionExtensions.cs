using Binance.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Exchange.Binance;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBinanceWrapper(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddSingleton(provider =>
        {
            var binanceRestClient = provider.GetRequiredService<BinanceRestClient>();
            return new BinanceRestClientSpotApiWrapper(binanceRestClient.SpotApi.Account,
                                                       binanceRestClient.SpotApi.ExchangeData,
                                                       binanceRestClient.SpotApi.Trading);
        });

        services.AddSingleton(provider =>
        {
            var binanceRestClient = provider.GetRequiredService<BinanceRestClient>();
            return new BinanceRestClientUsdFuturesApiWrapper(binanceRestClient.UsdFuturesApi.Account,
                                                             binanceRestClient.UsdFuturesApi.ExchangeData,
                                                             binanceRestClient.UsdFuturesApi.Trading);
        });
        services.AddSingleton(provider =>
        {
            var spotApiWrapper = provider.GetRequiredService<BinanceRestClientSpotApiWrapper>();
            var futuresApiWrapper = provider.GetRequiredService<BinanceRestClientUsdFuturesApiWrapper>();
            return new BinanceRestClientWrapper(spotApiWrapper, futuresApiWrapper);
        });

        services.AddSingleton(provider =>
        {
            var binanceSocketClient = provider.GetRequiredService<BinanceSocketClient>();
            return new BinanceSocketClientSpotApiWrapper(binanceSocketClient.SpotApi.Account,
                                                         binanceSocketClient.SpotApi.ExchangeData,
                                                         binanceSocketClient.SpotApi.Trading);
        });

        services.AddSingleton(provider =>
        {
            var binanceSocketClient = provider.GetRequiredService<BinanceSocketClient>();
            return new BinanceSocketClientUsdFuturesApiWrapper(binanceSocketClient.UsdFuturesApi.Account,
                                                               binanceSocketClient.UsdFuturesApi.ExchangeData,
                                                               binanceSocketClient.UsdFuturesApi.Trading);
        });
        services.AddSingleton(provider =>
        {
            var spotApiWrapper = provider.GetRequiredService<BinanceSocketClientSpotApiWrapper>();
            var futuresApiWrapper = provider.GetRequiredService<BinanceSocketClientUsdFuturesApiWrapper>();
            return new BinanceSocketClientWrapper(spotApiWrapper, futuresApiWrapper);
        });

        return services;
    }
}
