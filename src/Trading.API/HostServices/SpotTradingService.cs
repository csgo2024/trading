using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class SpotTradingService : BaseTradingService<Strategy>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly BinanceRestClient _binanceClient;

    public SpotTradingService(IStrategyRepository strategyRepository, ILogger<SpotTradingService> logger, BinanceRestClient binanceClient) : base(logger)
    {
        _binanceClient = binanceClient;
        _strategyRepository = strategyRepository;
    }

    protected override async Task<Dictionary<string, Strategy>?> InitializeStrategies()
    {
        return await _strategyRepository.InitializeSpotStrategies();
    }

    protected override async Task<WebCallResult<BinanceOrderBase>> GetOrderAsync(string symbol, long? orderId = null, string? origClientOrderId = null, long? receiveWindow = null,
        CancellationToken ct = default)
    {
        var webCallResult = await _binanceClient.SpotApi.Trading.GetOrderAsync(
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
        var result = new WebCallResult<BinanceOrderBase>(null, null, null, null, null, null, null, null, null, null, dataSource: ResultDataSource.Server, data: data, error: webCallResult.Error);
        return result;
    }

    protected override async Task<WebCallResult<IEnumerable<IBinanceKline>>> GetKlinesAsync(string symbol, KlineInterval interval, DateTime? startTime = null, DateTime? endTime = null,
        int? limit = null, CancellationToken ct = default(CancellationToken))
    {
        return await _binanceClient.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime, limit, ct);
    }

    protected override async Task<WebCallResult<BinanceOrderBase>> PlaceOrderAsync(string symbol, decimal? quantity = null, decimal? quoteQuantity = null,
        string? newClientOrderId = null, decimal? price = null, TimeInForce? timeInForce = null, decimal? stopPrice = null,
        decimal? icebergQty = null, OrderResponseType? orderResponseType = null, int? trailingDelta = null,
        int? strategyId = null, int? strategyType = null, SelfTradePreventionMode? selfTradePreventionMode = null,
        int? receiveWindow = null, CancellationToken ct = default(CancellationToken))
    {
        var webCallResult = await _binanceClient.SpotApi.Trading.PlaceOrderAsync(
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
        var result = new WebCallResult<BinanceOrderBase>(null, null, null, null, null, null, null, null, null, null, dataSource: ResultDataSource.Server, data: data, error: webCallResult.Error);
        return result;
    }

    protected override async Task<WebCallResult<BinanceOrderBase>> CancelOrderAsync(string symbol, long orderId, CancellationToken ct = default(CancellationToken))
    {
        var webCallResult = await _binanceClient.SpotApi.Trading.CancelOrderAsync(
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
        var result = new WebCallResult<BinanceOrderBase>(null, null, null, null, null, null, null, null, null, null, dataSource: ResultDataSource.Server, data: data, error: webCallResult.Error);
        return result;
    }

    protected override async Task<bool> UpdateStrategyOrderAsync(Strategy strategy, CancellationToken ct = default)
    {
        return await _strategyRepository.UpdateOrderStatusAsync(strategy, ct);
    }

    protected override async Task<(BinanceSymbolPriceFilter?, BinanceSymbolLotSizeFilter?)> GetSymbolFilterData(Strategy strategy, CancellationToken ct = default)
    {
        var exchangeInfo = await _binanceClient.SpotApi.ExchangeData.GetExchangeInfoAsync(ct: ct);
        if (!exchangeInfo.Success)
        {
            throw new InvalidOperationException($"[{strategy.StrategyType}-{strategy.Symbol}] Failed to get symbol filterData info.");

        }

        var symbolInfo = exchangeInfo.Data.Symbols.FirstOrDefault(s => s.Name == strategy.Symbol);
        if (symbolInfo == null)
        {
            throw new InvalidOperationException($"[{strategy.StrategyType}-{strategy.Symbol}] not found.");
        }
        var priceFilter = symbolInfo.PriceFilter;
        var lotSizeFilter = symbolInfo.LotSizeFilter;
        return (priceFilter, lotSizeFilter);
    }
}