using Binance.Net.Interfaces.Clients.UsdFuturesApi;

namespace Trading.Exchange.Binance.Wrappers.Clients;
public class BinanceSocketClientUsdFuturesApiWrapper
{
    /// <summary>
    /// Account streams and queries
    /// </summary>
    public IBinanceSocketClientUsdFuturesApiAccount Account { get; }
    /// <summary>
    /// Exchange data streams and queries
    /// </summary>
    public IBinanceSocketClientUsdFuturesApiExchangeData ExchangeData { get; }
    /// <summary>
    /// Trading data and queries
    /// </summary>
    public IBinanceSocketClientUsdFuturesApiTrading Trading { get; }

    public BinanceSocketClientUsdFuturesApiWrapper(IBinanceSocketClientUsdFuturesApiAccount account,
                                                   IBinanceSocketClientUsdFuturesApiExchangeData exchangeData,
                                                   IBinanceSocketClientUsdFuturesApiTrading trading)
    {
        Account = account;
        ExchangeData = exchangeData;
        Trading = trading;
    }
}
