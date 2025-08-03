using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Trading.Domain.Entities;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Services.Trading.Account;

public class SpotProcessor : IAccountProcessor
{
    private readonly BinanceRestClientSpotApiWrapper _spotApiClient;

    public SpotProcessor(BinanceRestClientSpotApiWrapper spotApiClient)
    {
        _spotApiClient = spotApiClient;
    }

    public async Task<WebCallResult<BinanceOrderBase>> GetOrder(string symbol, long? orderId, CancellationToken ct)
    {
        var webCallResult = await _spotApiClient.Trading.GetOrderAsync(
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

    public async Task<WebCallResult<IBinanceKline[]>> GetKlines(string symbol,
        KlineInterval interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken ct = default(CancellationToken))
    {
        return await _spotApiClient.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime, limit, ct);
    }

    public async Task<WebCallResult<BinanceOrderBase>> PlaceLongOrderAsync(string symbol,
        decimal quantity,
        decimal price,
        TimeInForce timeInForce,
        CancellationToken ct)
    {
        var webCallResult = await _spotApiClient.Trading.PlaceOrderAsync(
            symbol,
            OrderSide.Buy,
            SpotOrderType.Limit,
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

    public async Task<WebCallResult<BinanceOrderBase>> CancelOrderAsync(string symbol, long orderId, CancellationToken ct)
    {
        var webCallResult = await _spotApiClient.Trading.CancelOrderAsync(
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
        var exchangeInfo = await _spotApiClient.ExchangeData.GetExchangeInfoAsync(returnPermissionSets: null, symbolStatus: null, ct);
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

    public Task<WebCallResult<BinanceOrderBase>> PlaceShortOrderAsync(string symbol,
                                                                      decimal quantity,
                                                                      decimal price,
                                                                      TimeInForce timeInForce,
                                                                      CancellationToken ct)
    {
        throw new NotImplementedException("Spot account don't support short order.");
    }

    public Task<WebCallResult<BinanceOrderBase>> StopLongOrderAsync(string symbol,
                                                                     decimal quantity,
                                                                     decimal price,
                                                                     CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<WebCallResult<BinanceOrderBase>> StopShortOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
