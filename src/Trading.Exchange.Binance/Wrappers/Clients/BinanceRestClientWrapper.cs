namespace Trading.Exchange.Binance.Wrappers.Clients;

public class BinanceRestClientWrapper
{
    public readonly BinanceRestClientSpotApiWrapper SpotApi;
    public readonly BinanceRestClientUsdFuturesApiWrapper UsdFuturesApi;

    public BinanceRestClientWrapper(BinanceRestClientSpotApiWrapper spotApi,
                                    BinanceRestClientUsdFuturesApiWrapper usdFuturesApi)
    {
        SpotApi = spotApi;
        UsdFuturesApi = usdFuturesApi;
    }
}
