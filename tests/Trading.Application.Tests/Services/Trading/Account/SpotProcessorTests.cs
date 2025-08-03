using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.SpotApi;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;
using Trading.Exchange.Binance.Wrappers.Clients;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Tests.Services.Trading.Account;

public class SpotProcessorTests
{
    private readonly Mock<IBinanceRestClientSpotApiTrading> _mockTrading;
    private readonly Mock<IBinanceRestClientSpotApiExchangeData> _mockExchangeData;
    private readonly SpotProcessor _processor;
    private const string DefaultSymbol = "BTCUSDT";
    private const decimal DefaultQuantity = 1.0m;
    private const decimal DefaultPrice = 50000m;

    public SpotProcessorTests()
    {
        _mockTrading = new Mock<IBinanceRestClientSpotApiTrading>();
        _mockExchangeData = new Mock<IBinanceRestClientSpotApiExchangeData>();

        var mockAccount = new Mock<IBinanceRestClientSpotApiAccount>();
        var spotApiRestClient = new BinanceRestClientSpotApiWrapper(
            mockAccount.Object,
            _mockExchangeData.Object,
            _mockTrading.Object);
        _processor = new SpotProcessor(spotApiRestClient);
    }

    private static BinanceExchangeInfo CreateTestExchangeInfo(
        string symbol = "BTCUSDT",
        decimal tickSize = 0.01m,
        decimal stepSize = 0.00001m)
    {
        return new BinanceExchangeInfo
        {
            Symbols =
            [
                new BinanceSymbol
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
                            MinQuantity = 0.00001m,
                            MaxQuantity = 100000m
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
        var expectedOrder = new BinanceOrder
        {
            Id = 12345,
            Status = OrderStatus.Filled
        };
        _mockTrading.SetupSuccessfulGetOrderAsync(expectedOrder);

        // Act
        var result = await _processor.GetOrder(DefaultSymbol, 12345, CancellationToken.None);

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
        var expectedOrder = new BinancePlacedOrder
        {
            Id = 12345,
            Status = OrderStatus.New
        };
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
    public async Task CancelOrderAsync_WhenSuccessful_ShouldReturnCanceledOrder()
    {
        // Arrange
        var expectedOrder = new BinanceOrderBase
        {
            Id = 12345,
            Status = OrderStatus.Canceled
        };
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
        var strategy = new Strategy { Symbol = DefaultSymbol, AccountType = AccountType.Spot };
        var exchangeInfo = CreateTestExchangeInfo();
        _mockExchangeData.SetupSuccessfulGetExchangeInfoAsync(exchangeInfo);

        // Act
        var (priceFilter, lotSizeFilter) = await _processor.GetSymbolFilterData(strategy);

        // Assert
        Assert.NotNull(priceFilter);
        Assert.NotNull(lotSizeFilter);
        Assert.Equal(0.01m, priceFilter.TickSize);
        Assert.Equal(0.00001m, lotSizeFilter.StepSize);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenFailed_ShouldThrowException()
    {
        // Arrange
        var strategy = new Strategy { Symbol = DefaultSymbol, AccountType = AccountType.Spot };
        var error = new ServerError("Server error");
        _mockExchangeData.SetupFailedGetExchangeInfoAsync(error);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.GetSymbolFilterData(strategy));
        Assert.Contains($"[{strategy.AccountType}-{strategy.Symbol}] Failed to get symbol filterData info",
            exception.Message);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenSymbolNotFound_ShouldThrowException()
    {
        // Arrange
        var strategy = new Strategy { Symbol = "UNKNOWN", AccountType = AccountType.Spot };
        var exchangeInfo = CreateTestExchangeInfo();
        _mockExchangeData.SetupSuccessfulGetExchangeInfoAsync(exchangeInfo);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.GetSymbolFilterData(strategy));
    }
    [Fact]
    public async Task PlaceShortOrderAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _processor.PlaceShortOrderAsync("BTCUSDT", 1m, 50000m, TimeInForce.GoodTillCanceled, CancellationToken.None));
    }
    [Fact]
    public async Task StopLongOrderAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _processor.StopLongOrderAsync("BTCUSDT", 1m, 50000m, CancellationToken.None));
    }
    [Fact]
    public async Task StopShortOrderAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _processor.StopShortOrderAsync("BTCUSDT", 1m, 50000m, CancellationToken.None));
    }
}
