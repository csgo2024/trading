using Binance.Net.Interfaces.Clients.SpotApi;

namespace Trading.Exchange.Binance.Wrappers.Clients;

public class BinanceRestClientSpotApiWrapper
{
    public IBinanceRestClientSpotApiAccount Account { get; }

    /// <summary>
    /// Endpoints related to retrieving market and system data
    /// </summary>
    public IBinanceRestClientSpotApiExchangeData ExchangeData { get; }

    /// <summary>
    /// Endpoints related to orders and trades
    /// </summary>
    public IBinanceRestClientSpotApiTrading Trading { get; }

    public BinanceRestClientSpotApiWrapper(IBinanceRestClientSpotApiAccount account,
                                           IBinanceRestClientSpotApiExchangeData exchangeData,
                                           IBinanceRestClientSpotApiTrading trading)
    {
        Account = account;
        ExchangeData = exchangeData;
        Trading = trading;
    }

}
