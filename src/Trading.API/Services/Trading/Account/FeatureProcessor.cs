using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Trading.Domain.Entities;

namespace Trading.API.Services.Trading.Account;

public class BinanceFeatureRestClientWrapper
{
    public readonly IBinanceRestClientUsdFuturesApiTrading Trading;
    public readonly IBinanceRestClientUsdFuturesApiExchangeData ExchangeData;
    public BinanceFeatureRestClientWrapper(IBinanceRestClientUsdFuturesApiTrading trading,
        IBinanceRestClientUsdFuturesApiExchangeData exchangeData
    )
    {
        Trading = trading;
        ExchangeData = exchangeData;
    }
}

public class FeatureProcessor : IAccountProcessor
{
    private readonly BinanceFeatureRestClientWrapper _binanceClient;

    public FeatureProcessor(BinanceFeatureRestClientWrapper binanceClient)
    {
        _binanceClient = binanceClient;
    }

    public async Task<WebCallResult<BinanceOrderBase>> GetOrder(string symbol, long? orderId, CancellationToken ct)
    {
        var webCallResult = await _binanceClient.Trading.GetOrderAsync(
            symbol: symbol,
            orderId: orderId,
            ct: ct);
        if (!webCallResult.Success)
        {
            return new WebCallResult<BinanceOrderBase>(webCallResult.Error);
        }

        var data = new BinanceOrderBase
        {
            Id = webCallResult.Data.Id,
            Status = webCallResult.Data.Status,
        };
        var result = new WebCallResult<BinanceOrderBase>(null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            dataSource: ResultDataSource.Server,
            data: data,
            error: webCallResult.Error);
        return result;
    }

    public async Task<WebCallResult<IEnumerable<IBinanceKline>>> GetKlines(string symbol,
        KlineInterval interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken ct = default(CancellationToken))
    {
        return await _binanceClient.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime, limit, ct);
    }

    public async Task<WebCallResult<BinanceOrderBase>> PlaceOrder(string symbol,
        decimal quantity,
        decimal price,
        TimeInForce timeInForce,
        CancellationToken ct)
    {
        var webCallResult = await _binanceClient.Trading.PlaceOrderAsync(
            symbol: symbol,
            side: OrderSide.Buy,
            type: FuturesOrderType.Limit,
            positionSide: PositionSide.Long,
            quantity: quantity,
            price: price,
            timeInForce: TimeInForce.GoodTillCanceled,
            ct: ct);
        if (!webCallResult.Success)
        {
            return new WebCallResult<BinanceOrderBase>(webCallResult.Error);
        }
        var data = new BinanceOrderBase
        {
            Id = webCallResult.Data.Id,
            Status = webCallResult.Data.Status,
        };
        var result = new WebCallResult<BinanceOrderBase>(null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         null,
                                                         dataSource: ResultDataSource.Server,
                                                         data: data,
                                                         error: webCallResult.Error);
        return result;

    }

    public async Task<WebCallResult<BinanceOrderBase>> CancelOrder(string symbol, long orderId, CancellationToken ct)
    {
        var webCallResult = await _binanceClient.Trading.CancelOrderAsync(
            symbol: symbol,
            orderId: orderId,
            ct: ct);

        if (!webCallResult.Success)
        {
            return new WebCallResult<BinanceOrderBase>(webCallResult.Error);
        }
        var data = new BinanceOrderBase
        {
            Id = webCallResult.Data.Id,
            Status = webCallResult.Data.Status,
        };
        var result = new WebCallResult<BinanceOrderBase>(null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            dataSource: ResultDataSource.Server,
            data: data,
            error: webCallResult.Error);
        return result;
    }

    public async Task<(BinanceSymbolPriceFilter?, BinanceSymbolLotSizeFilter?)> GetSymbolFilterData(Strategy strategy, CancellationToken ct = default)
    {
        var exchangeInfo = await _binanceClient.ExchangeData.GetExchangeInfoAsync(ct: ct);
        if (!exchangeInfo.Success)
        {
            throw new InvalidOperationException($"[{strategy.AccountType}-{strategy.Symbol}] Failed to get symbol filterData info.");

        }

        var symbolInfo = exchangeInfo.Data.Symbols.FirstOrDefault(s => s.Name == strategy.Symbol);
        if (symbolInfo == null)
        {
            throw new InvalidOperationException($"[{strategy.AccountType}-{strategy.Symbol}] not found.");
        }
        var priceFilter = symbolInfo.PriceFilter;
        var lotSizeFilter = symbolInfo.LotSizeFilter;
        return (priceFilter, lotSizeFilter);
    }
}