using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.SpotApi;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.Application.Tests;

public static class TestExtensions
{
    public static void SetupSuccessfulSymbolFilter(this Mock<IAccountProcessor> mockAccountProcessor)
    {
        mockAccountProcessor
            .Setup(x => x.GetSymbolFilterData(
                It.IsAny<Strategy>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new BinanceSymbolPriceFilter
            {
                TickSize = 0.001m,
                MaxPrice = decimal.MaxValue,
                MinPrice = decimal.MinValue,
            }, new BinanceSymbolLotSizeFilter
            {
                StepSize = 0.01m,
                MinQuantity = decimal.MinValue,
                MaxQuantity = decimal.MaxValue,
            }));
    }

    public static void SetupSuccessfulPlaceLongOrderAsync(this Mock<IAccountProcessor> mockAccountProcessor, long orderId)
    {
        var result = CreateSuccessResult(new BinanceOrderBase { Id = orderId });
        mockAccountProcessor
            .Setup(x => x.PlaceLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupFailedPlaceLongOrderAsync(this Mock<IAccountProcessor> mockAccountProcessor, string message)
    {
        var result = CreateFailedResult(new BinanceOrderBase { }, message);
        mockAccountProcessor
            .Setup(x => x.PlaceLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupSuccessfulStopLongOrderAsync(this Mock<IAccountProcessor> mockAccountProcessor)
    {
        var result = CreateSuccessResult(new BinanceOrderBase { });
        mockAccountProcessor
            .Setup(x => x.StopLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupFailedStopLongOrderAsync(this Mock<IAccountProcessor> mockAccountProcessor, string message)
    {
        var result = CreateFailedResult(new BinanceOrderBase { }, message);
        mockAccountProcessor
            .Setup(x => x.StopLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupSuccessfulPlaceShortOrderAsync(this Mock<IAccountProcessor> mockAccountProcessor, long orderId)
    {
        var result = CreateSuccessResult(new BinanceOrderBase { Id = orderId });
        mockAccountProcessor
            .Setup(x => x.PlaceShortOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    public static void SetupSuccessfulGetOrder(this Mock<IAccountProcessor> mockAccountProcessor, OrderStatus status)
    {
        var result = CreateSuccessResult(new BinanceOrderBase { Status = status });
        mockAccountProcessor
            .Setup(x => x.GetOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupFailedGetOrder(this Mock<IAccountProcessor> mockAccountProcessor, string message)
    {
        var result = CreateFailedResult(new BinanceOrderBase { }, message);
        mockAccountProcessor
            .Setup(x => x.GetOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupSuccessfulCancelOrder(this Mock<IAccountProcessor> mockAccountProcessor)
    {
        var result = CreateSuccessResult(new BinanceOrderBase { });
        mockAccountProcessor
            .Setup(x => x.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }
    public static void SetupFailedCancelOrder(this Mock<IAccountProcessor> mockAccountProcessor, string message)
    {
        var result = CreateFailedResult(new BinanceOrderBase { }, message);
        mockAccountProcessor
            .Setup(x => x.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    public static void SetupSuccessfulGetKlines(this Mock<IAccountProcessor> mockAccountProcessor,
        decimal openPrice = 40000m,
        decimal closePrice = 41000m,
        decimal highPrice = 42000m,
        decimal lowPrice = 39000m)
    {
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == openPrice &&
            k.ClosePrice == closePrice &&
            k.HighPrice == highPrice &&
            k.LowPrice == lowPrice);

        mockAccountProcessor
            .Setup(x => x.GetKlines(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult<IBinanceKline[]>([kline]));
    }

    public static void SetupFailedGetKlines(this Mock<IAccountProcessor> mockAccountProcessor)
    {
        var kline = Mock.Of<IBinanceKline>(k => k.LowPrice == 10000m);
        mockAccountProcessor
            .Setup(x => x.GetKlines(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFailedResult<IBinanceKline[]>([kline], "Server Error."));
    }
    public static void VerifyLoggingTimes<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage, Times time) where T : class
    {
        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(x => x == logLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            time);
    }
    public static void VerifyLoggingTimes<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage, Func<Times> time) where T : class
    {
        VerifyLoggingTimes(logger, logLevel, expectedMessage, time());
    }
    public static void VerifyLoggingNever<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage = "") where T : class
    {
        VerifyLoggingTimes(logger, logLevel, expectedMessage, Times.Never);
    }
    public static void VerifyLoggingOnce<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage) where T : class
    {
        VerifyLoggingTimes(logger, logLevel, expectedMessage, Times.Once);
    }

    private static WebCallResult<T> CreateSuccessResult<T>(T data) where T : class
    {
        return new WebCallResult<T>(null, null, null, 0, null, 0, null, null, null, null, ResultDataSource.Server, data, null);
    }
    private static WebCallResult<T> CreateFailedResult<T>(T data, string message) where T : class
    {
        return new WebCallResult<T>(null, null, null, 0, null, 0, null, null, null, null, ResultDataSource.Server, data, new ServerError(message));
    }

    #region Futures API

    public static void SetupSuccessfulGetKlinesAsync(
        this Mock<IBinanceRestClientUsdFuturesApiExchangeData> mockExchangeData,
        IBinanceKline[] klines)
    {
        mockExchangeData
            .Setup(x => x.GetKlinesAsync(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(klines));
    }

    public static void SetupSuccessfulGetOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        BinanceUsdFuturesOrder order)
    {
        mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(order));
    }

    public static void SetupFailedGetOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        ServerError error)
    {
        mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(error));
    }

    public static void SetupSuccessfulPlaceOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        BinanceUsdFuturesOrder order)
    {
        mockTrading
            .Setup(m => m.PlaceOrderAsync(
                It.IsAny<string>(),
                It.IsAny<OrderSide>(),
                It.IsAny<FuturesOrderType>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<PositionSide?>(),
                It.IsAny<TimeInForce?>(),
                It.IsAny<bool?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<WorkingType?>(),
                It.IsAny<bool?>(),
                It.IsAny<OrderResponseType?>(),
                It.IsAny<bool?>(),
                It.IsAny<PriceMatch?>(),
                It.IsAny<SelfTradePreventionMode?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(order));
    }

    public static void SetupFailedPlaceOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        ServerError error)
    {
        mockTrading
            .Setup(m => m.PlaceOrderAsync(
                It.IsAny<string>(),
                It.IsAny<OrderSide>(),
                It.IsAny<FuturesOrderType>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<PositionSide?>(),
                It.IsAny<TimeInForce?>(),
                It.IsAny<bool?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<WorkingType?>(),
                It.IsAny<bool?>(),
                It.IsAny<OrderResponseType?>(),
                It.IsAny<bool?>(),
                It.IsAny<PriceMatch?>(),
                It.IsAny<SelfTradePreventionMode?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(error));
    }

    public static void SetupSuccessfulCancelOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        BinanceUsdFuturesOrder order)
    {
        mockTrading
            .Setup(x => x.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(order));
    }

    public static void SetupFailedCancelOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        ServerError error)
    {
        mockTrading
            .Setup(x => x.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(error));
    }

    public static void SetupGetExchangeInfoAsync(
        this Mock<IBinanceRestClientUsdFuturesApiExchangeData> mockExchangeData,
        BinanceFuturesUsdtExchangeInfo exchangeInfo)
    {
        mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(exchangeInfo));
    }

    public static void SetupGetExchangeInfoAsyncError(
        this Mock<IBinanceRestClientUsdFuturesApiExchangeData> mockExchangeData,
        ServerError error)
    {
        mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceFuturesUsdtExchangeInfo>(error));
    }

    public static void VerifyPlaceOrderAsync(
        this Mock<IBinanceRestClientUsdFuturesApiTrading> mockTrading,
        string symbol,
        OrderSide side,
        FuturesOrderType orderType,
        decimal quantity,
        decimal? price,
        PositionSide positionSide,
        TimeInForce timeInForce,
        bool? reduceOnly = null)
    {
        mockTrading.Verify(x => x.PlaceOrderAsync(
            symbol,
            side,
            orderType,
            quantity,
            price,
            positionSide,
            timeInForce,
            reduceOnly,
            null, null, null, null, null, null, null, null, null, null, null, null,
            It.IsAny<CancellationToken>()),
        Times.Once);
    }
    #endregion

    #region Spot API
    public static void SetupSuccessfulGetKlinesAsync(
        this Mock<IBinanceRestClientSpotApiExchangeData> mockExchangeData,
        IBinanceKline[] klines)
    {
        mockExchangeData
            .Setup(x => x.GetKlinesAsync(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(klines));
    }

    public static void SetupSuccessfulGetOrderAsync(
        this Mock<IBinanceRestClientSpotApiTrading> mockTrading,
        BinanceOrder order)
    {
        mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(order));
    }

    public static void SetupFailedGetOrderAsync(
        this Mock<IBinanceRestClientSpotApiTrading> mockTrading,
        ServerError error)
    {
        mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrder>(error));
    }

    public static void SetupSuccessfulPlaceOrderAsync(
        this Mock<IBinanceRestClientSpotApiTrading> mockTrading,
        BinancePlacedOrder order)
    {
        mockTrading
            .Setup(m => m.PlaceOrderAsync(
                It.IsAny<string>(),
                It.IsAny<OrderSide>(),
                It.IsAny<SpotOrderType>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<TimeInForce?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<OrderResponseType?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SelfTradePreventionMode?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(order));
    }

    public static void SetupFailedPlaceOrderAsync(
        this Mock<IBinanceRestClientSpotApiTrading> mockTrading,
        ServerError error)
    {
        mockTrading
            .Setup(m => m.PlaceOrderAsync(
                It.IsAny<string>(),
                It.IsAny<OrderSide>(),
                It.IsAny<SpotOrderType>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<TimeInForce?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<OrderResponseType?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SelfTradePreventionMode?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinancePlacedOrder>(error));
    }

    public static void SetupSuccessfulCancelOrderAsync(
        this Mock<IBinanceRestClientSpotApiTrading> mockTrading,
        BinanceOrderBase order)
    {
        mockTrading
            .Setup(m => m.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancelRestriction?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(order));
    }

    public static void SetupFailedCancelOrderAsync(
        this Mock<IBinanceRestClientSpotApiTrading> mockTrading,
        ServerError error)
    {
        mockTrading
            .Setup(m => m.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancelRestriction?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(error));
    }

    public static void SetupSuccessfulGetExchangeInfoAsync(
        this Mock<IBinanceRestClientSpotApiExchangeData> mockExchangeData,
        BinanceExchangeInfo exchangeInfo)
    {
        mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(
                It.IsAny<bool?>(),
                It.IsAny<SymbolStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult(exchangeInfo));
    }

    public static void SetupFailedGetExchangeInfoAsync(
        this Mock<IBinanceRestClientSpotApiExchangeData> mockExchangeData,
        ServerError error)
    {
        mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(
                It.IsAny<bool?>(),
                It.IsAny<SymbolStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceExchangeInfo>(error));
    }

    #endregion
}
