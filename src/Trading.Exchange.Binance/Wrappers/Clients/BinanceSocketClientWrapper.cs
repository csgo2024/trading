namespace Trading.Exchange.Binance.Wrappers.Clients;

public class BinanceSocketClientWrapper
{
    public readonly BinanceSocketClientSpotApiWrapper SpotApi;
    public readonly BinanceSocketClientUsdFuturesApiWrapper UsdFuturesApi;

    public BinanceSocketClientWrapper(BinanceSocketClientSpotApiWrapper spotApi,
                                      BinanceSocketClientUsdFuturesApiWrapper usdFuturesApi)
    {
        SpotApi = spotApi;
        UsdFuturesApi = usdFuturesApi;
    }
}
