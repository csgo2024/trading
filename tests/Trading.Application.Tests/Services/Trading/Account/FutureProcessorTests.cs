using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;
using Trading.Exchange.Binance.Wrappers.Clients;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Tests.Services.Trading.Account;

public class FutureProcessorTests
{
    private readonly Mock<IBinanceRestClientUsdFuturesApiTrading> _mockTrading;
    private readonly Mock<IBinanceRestClientUsdFuturesApiExchangeData> _mockExchangeData;
    private readonly FutureProcessor _processor;
    private const string DefaultSymbol = "BTCUSDT";
    private const decimal DefaultQuantity = 1.0m;
    private const decimal DefaultPrice = 50000m;

    public FutureProcessorTests()
    {
        var mockAccount = new Mock<IBinanceRestClientUsdFuturesApiAccount>();
        _mockTrading = new Mock<IBinanceRestClientUsdFuturesApiTrading>();
        _mockExchangeData = new Mock<IBinanceRestClientUsdFuturesApiExchangeData>();

        var binanceClient = new BinanceRestClientUsdFuturesApiWrapper(
            mockAccount.Object,
            _mockExchangeData.Object,
            _mockTrading.Object);
        _processor = new FutureProcessor(binanceClient);
    }
    private static BinanceUsdFuturesOrder CreateTestOrder(long orderId = 12345, OrderStatus status = OrderStatus.New)
    {
        return new BinanceUsdFuturesOrder
        {
            Id = orderId,
            Status = status
        };
    }
    private static BinanceFuturesUsdtExchangeInfo CreateTestExchangeInfo(string symbol = "BTCUSDT", decimal tickSize = 0.01m, decimal stepSize = 0.001m)
    {
        return new BinanceFuturesUsdtExchangeInfo
        {
            Symbols =
            [
                new BinanceFuturesUsdtSymbol
                {
                    Name = symbol,
                    Filters =
                    [
                        new BinanceSymbolPriceFilter
                        {
                            TickSize = tickSize,
                            MinPrice = 0.01m,
                            MaxPrice = 100000m
                        },
                        new BinanceSymbolLotSizeFilter
                        {
                            StepSize = stepSize,
                            MinQuantity = 0.001m,
                            MaxQuantity = 1000m
                        }
                    ]
                }
            ]
        };
    }

    [Fact]
    public async Task GetOrder_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = CreateTestOrder();
        _mockTrading.SetupSuccessfulGetOrderAsync(expectedOrder);

        // Act
        var result = await _processor.GetOrder(DefaultSymbol, expectedOrder.Id, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);
    }

    [Fact]
    public async Task GetOrder_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var error = new ServerError("Test error");
        _mockTrading.SetupFailedGetOrderAsync(error);

        // Act
        var result = await _processor.GetOrder(DefaultSymbol, 12345, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task GetKlines_ShouldPassThroughToClient()
    {
        // Arrange
        var expectedKlines = Array.Empty<IBinanceKline>();
        _mockExchangeData.SetupSuccessfulGetKlinesAsync(expectedKlines);

        // Act
        var result = await _processor.GetKlines(
            DefaultSymbol,
            KlineInterval.OneMinute,
            DateTime.UtcNow,
            null,
            100,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedKlines, result.Data);
    }

    [Fact]
    public async Task PlaceLongOrderAsync_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = CreateTestOrder();
        _mockTrading.SetupSuccessfulPlaceOrderAsync(expectedOrder);

        // Act
        var result = await _processor.PlaceLongOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            TimeInForce.GoodTillCanceled,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);

        _mockTrading.VerifyPlaceOrderAsync(
            DefaultSymbol,
            OrderSide.Buy,
            FuturesOrderType.Limit,
            DefaultQuantity,
            DefaultPrice,
            PositionSide.Long,
            TimeInForce.GoodTillCanceled);
    }

    [Fact]
    public async Task PlaceLongOrderAsync_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var error = new ServerError("Test error");
        _mockTrading.SetupFailedPlaceOrderAsync(error);

        // Act
        var result = await _processor.PlaceLongOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            TimeInForce.GoodTillCanceled,
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task PlaceShortOrderAsync_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = CreateTestOrder();
        _mockTrading.SetupSuccessfulPlaceOrderAsync(expectedOrder);

        // Act
        var result = await _processor.PlaceShortOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            TimeInForce.GoodTillCanceled,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);

        _mockTrading.VerifyPlaceOrderAsync(
            DefaultSymbol,
            OrderSide.Sell,
            FuturesOrderType.Limit,
            DefaultQuantity,
            DefaultPrice,
            PositionSide.Short,
            TimeInForce.GoodTillCanceled);
    }

    [Fact]
    public async Task PlaceShortOrderAsync_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var error = new ServerError("Test error");
        _mockTrading.SetupFailedPlaceOrderAsync(error);

        // Act
        var result = await _processor.PlaceShortOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            TimeInForce.GoodTillCanceled,
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenSuccessful_ShouldReturnCanceledOrder()
    {
        // Arrange
        var expectedOrder = CreateTestOrder(status: OrderStatus.Canceled);
        _mockTrading.SetupSuccessfulCancelOrderAsync(expectedOrder);

        // Act
        var result = await _processor.CancelOrderAsync(DefaultSymbol, 12345, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var error = new ServerError("Test error");
        _mockTrading.SetupFailedCancelOrderAsync(error);

        // Act
        var result = await _processor.CancelOrderAsync(DefaultSymbol, 12345, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenSuccessful_ShouldReturnFilters()
    {
        // Arrange
        var strategy = new Strategy { Symbol = DefaultSymbol, AccountType = AccountType.Future };
        var exchangeInfo = CreateTestExchangeInfo();
        _mockExchangeData.SetupGetExchangeInfoAsync(exchangeInfo);

        // Act
        var (priceFilter, lotSizeFilter) = await _processor.GetSymbolFilterData(strategy);

        // Assert
        Assert.NotNull(priceFilter);
        Assert.NotNull(lotSizeFilter);
        Assert.Equal(0.01m, priceFilter.TickSize);
        Assert.Equal(0.001m, lotSizeFilter.StepSize);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenFailed_ShouldThrowException()
    {
        // Arrange
        var strategy = new Strategy { Symbol = DefaultSymbol, AccountType = AccountType.Future };
        var error = new ServerError("Server error");
        _mockExchangeData.SetupGetExchangeInfoAsyncError(error);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.GetSymbolFilterData(strategy));
        Assert.Contains($"[{strategy.AccountType}-{strategy.Symbol}] Failed to get symbol filterData info", exception.Message);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenSymbolNotFound_ShouldThrowException()
    {
        // Arrange
        var strategy = new Strategy { Symbol = "UNKNOWN", AccountType = AccountType.Future };
        var exchangeInfo = CreateTestExchangeInfo();
        _mockExchangeData.SetupGetExchangeInfoAsync(exchangeInfo);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.GetSymbolFilterData(strategy));
        Assert.Contains($"[{strategy.AccountType}-{strategy.Symbol}]", exception.Message);
    }

    [Fact]
    public async Task StopLongOrderAsync_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = CreateTestOrder();
        _mockTrading.SetupSuccessfulPlaceOrderAsync(expectedOrder);

        // Act
        var result = await _processor.StopLongOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);

        _mockTrading.VerifyPlaceOrderAsync(
            DefaultSymbol,
            OrderSide.Sell,
            FuturesOrderType.StopMarket,
            DefaultQuantity,
            null,
            PositionSide.Short,
            TimeInForce.GoodTillCanceled,
            true);
    }

    [Fact]
    public async Task StopLongOrderAsync_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var error = new ServerError("Test error");
        _mockTrading.SetupFailedPlaceOrderAsync(error);

        // Act
        var result = await _processor.StopLongOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task StopShortOrderAsync_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = CreateTestOrder();
        _mockTrading.SetupSuccessfulPlaceOrderAsync(expectedOrder);

        // Act
        var result = await _processor.StopShortOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);

        _mockTrading.VerifyPlaceOrderAsync(
            DefaultSymbol,
            OrderSide.Buy,
            FuturesOrderType.StopMarket,
            DefaultQuantity,
            null,
            PositionSide.Long,
            TimeInForce.GoodTillCanceled,
            true);
    }

    [Fact]
    public async Task StopShortOrderAsync_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var error = new ServerError("Test error");
        _mockTrading.SetupFailedPlaceOrderAsync(error);

        // Act
        var result = await _processor.StopShortOrderAsync(
            DefaultSymbol,
            DefaultQuantity,
            DefaultPrice,
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
    }
}
