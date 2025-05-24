using Binance.Net.Interfaces.Clients.SpotApi;

namespace Trading.Exchange.Binance.Wrappers.Clients;
public class BinanceSocketClientSpotApiWrapper
{
    /// <summary>
    /// Account streams and queries
    /// </summary>
    public IBinanceSocketClientSpotApiAccount Account { get; }
    /// <summary>
    /// Exchange data streams and queries
    /// </summary>
    public IBinanceSocketClientSpotApiExchangeData ExchangeData { get; }
    /// <summary>
    /// Trading data and queries
    /// </summary>
    public IBinanceSocketClientSpotApiTrading Trading { get; }

    public BinanceSocketClientSpotApiWrapper(IBinanceSocketClientSpotApiAccount account,
                                             IBinanceSocketClientSpotApiExchangeData exchangeData,
                                             IBinanceSocketClientSpotApiTrading trading)
    {
        Account = account;
        ExchangeData = exchangeData;
        Trading = trading;
    }
}
