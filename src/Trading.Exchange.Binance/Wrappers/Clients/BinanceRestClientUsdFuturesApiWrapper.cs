using Binance.Net.Interfaces.Clients.UsdFuturesApi;

namespace Trading.Exchange.Binance.Wrappers.Clients;
public class BinanceRestClientUsdFuturesApiWrapper
{
    public IBinanceRestClientUsdFuturesApiAccount Account { get; }

    /// <summary>
    /// Endpoints related to retrieving market and system data
    /// </summary>
    public IBinanceRestClientUsdFuturesApiExchangeData ExchangeData { get; }

    /// <summary>
    /// Endpoints related to orders and trades
    /// </summary>
    public IBinanceRestClientUsdFuturesApiTrading Trading { get; }

    public BinanceRestClientUsdFuturesApiWrapper(IBinanceRestClientUsdFuturesApiAccount account,
                                                 IBinanceRestClientUsdFuturesApiExchangeData exchangeData,
                                                 IBinanceRestClientUsdFuturesApiTrading trading)
    {
        Account = account;
        ExchangeData = exchangeData;
        Trading = trading;
    }

}
