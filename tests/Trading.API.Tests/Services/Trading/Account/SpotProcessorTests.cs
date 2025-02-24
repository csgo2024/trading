using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.SpotApi;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Moq;
using Trading.API.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.API.Tests.Services.Trading.Account;

public class SpotProcessorTests
{
    private readonly BinanceSpotRestClientWrapper _binanceClient;
    private readonly Mock<IBinanceRestClientSpotApiTrading> _mockTrading;
    private readonly Mock<IBinanceRestClientSpotApiExchangeData> _mockExchangeData;
    private readonly SpotProcessor _processor;

    public SpotProcessorTests()
    {
        _mockTrading = new Mock<IBinanceRestClientSpotApiTrading>();
        _mockExchangeData = new Mock<IBinanceRestClientSpotApiExchangeData>();

        _binanceClient = new BinanceSpotRestClientWrapper(_mockTrading.Object, _mockExchangeData.Object);
        _processor = new SpotProcessor(_binanceClient);
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

        _mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrder>(null,
                                                          null,
                                                          TimeSpan.Zero,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          ResultDataSource.Server,
                                                          expectedOrder,
                                                          null));

        // Act
        var result = await _processor.GetOrder("BTCUSDT", 12345, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);
    }

    [Fact]
    public async Task GetOrder_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var expectedError = new ServerError("Test error");
        _mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrder>(expectedError));

        // Act
        var result = await _processor.GetOrder("BTCUSDT", 12345, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task GetKlines_ShouldPassThroughToClient()
    {
        // Arrange
        var expectedKlines = new List<IBinanceKline>();
        _mockExchangeData
            .Setup(x => x.GetKlinesAsync(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<IEnumerable<IBinanceKline>>(null,
                                                                        null,
                                                                        TimeSpan.Zero,
                                                                        null,
                                                                        null,
                                                                        null,
                                                                        null,
                                                                        null,
                                                                        null,
                                                                        null,
                                                                        ResultDataSource.Server,
                                                                        expectedKlines,
                                                                        null));

        // Act
        var result = await _processor.GetKlines(
            "BTCUSDT",
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
    public async Task PlaceOrder_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = new BinancePlacedOrder
        {
            Id = 12345,
            Status = OrderStatus.New
        };

        _mockTrading
            .Setup(m => m.PlaceOrderAsync(
            It.IsAny<string>(),    // 忽略 symbol
            It.IsAny<OrderSide>(), // 忽略 side
            It.IsAny<SpotOrderType>(), // 忽略 type
            It.IsAny<decimal?>(),  // 忽略 quantity
            It.IsAny<decimal?>(),  // 忽略 quoteQuantity
            It.IsAny<string?>(),   // 忽略 newClientOrderId
            It.IsAny<decimal?>(),  // 忽略 price
            It.IsAny<TimeInForce?>(),  // 忽略 timeInForce
            It.IsAny<decimal?>(),  // 忽略 stopPrice
            It.IsAny<decimal?>(),  // 忽略 icebergQty
            It.IsAny<OrderResponseType?>(), // 忽略 orderResponseType
            It.IsAny<int?>(),      // 忽略 trailingDelta
            It.IsAny<int?>(),      // 忽略 strategyId
            It.IsAny<int?>(),      // 忽略 strategyType
            It.IsAny<SelfTradePreventionMode?>(), // 忽略 selfTradePreventionMode
            It.IsAny<int?>(),      // 忽略 receiveWindow
            It.IsAny<CancellationToken>() // 忽略 ct
            )).ReturnsAsync(new WebCallResult<BinancePlacedOrder>(null,
                                                          null,
                                                          TimeSpan.Zero,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          null,
                                                          ResultDataSource.Server,
                                                          expectedOrder,
                                                          null));

        // Act
        var result = await _processor.PlaceOrder(
            "BTCUSDT",
            1.0m,
            50000m,
            TimeInForce.GoodTillCanceled,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);
    }

    [Fact]
    public async Task CancelOrder_WhenSuccessful_ShouldReturnCanceledOrder()
    {
        // Arrange
        var expectedOrder = new BinanceOrderBase
        {
            Id = 12345,
            Status = OrderStatus.Canceled
        };

        _mockTrading
            .Setup(m => m.CancelOrderAsync(
                It.IsAny<string>(),        // 忽略 symbol
                It.IsAny<long?>(),         // 忽略 orderId
                It.IsAny<string?>(),       // 忽略 origClientOrderId
                It.IsAny<string?>(),       // 忽略 newClientOrderId
                It.IsAny<CancelRestriction?>(), // 忽略 cancelRestriction
                It.IsAny<long?>(),         // 忽略 receiveWindow
                It.IsAny<CancellationToken>() // 忽略 ct
            ))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(null,
                                                                null,
                                                                TimeSpan.Zero,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                ResultDataSource.Server,
                                                                expectedOrder,
                                                                null)
            );

        // Act
        var result = await _processor.CancelOrder("BTCUSDT", 12345, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedOrder.Id, result.Data.Id);
        Assert.Equal(expectedOrder.Status, result.Data.Status);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenSuccessful_ShouldReturnFilters()
    {
        // Arrange
        var strategy = new Strategy { Symbol = "BTCUSDT", AccountType = Domain.Entities.AccountType.Spot };
        var expectedPriceFilter = new BinanceSymbolPriceFilter
        {
            TickSize = 0.01m,
            MinPrice = 0.01m,
            MaxPrice = 100000m
        };
        var expectedLotSizeFilter = new BinanceSymbolLotSizeFilter
        {
            StepSize = 0.00001m,
            MinQuantity = 0.00001m,
            MaxQuantity = 100000m
        };

        var exchangeInfo = new BinanceExchangeInfo
        {
            Symbols = new[]
            {
                new BinanceSymbol
                {
                    Name = "BTCUSDT",
                    Filters = new BinanceSymbolFilter[]
                    {
                        expectedPriceFilter,
                        expectedLotSizeFilter
                    }
                }
            }
        };

        _mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(
                It.IsAny<bool?>(),
                It.IsAny<SymbolStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceExchangeInfo>(null,
                                                                null,
                                                                TimeSpan.Zero,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                ResultDataSource.Server,
                                                                exchangeInfo,
                                                                null));

        // Act
        var (priceFilter, lotSizeFilter) = await _processor.GetSymbolFilterData(strategy);

        // Assert
        Assert.NotNull(priceFilter);
        Assert.NotNull(lotSizeFilter);
        Assert.Equal(expectedPriceFilter.TickSize, priceFilter.TickSize);
        Assert.Equal(expectedLotSizeFilter.StepSize, lotSizeFilter.StepSize);
    }

    [Fact]
    public async Task GetSymbolFilterData_WhenSymbolNotFound_ShouldThrowException()
    {
        // Arrange
        var strategy = new Strategy { Symbol = "UNKNOWN", AccountType = Domain.Entities.AccountType.Spot };
        var exchangeInfo = new BinanceExchangeInfo { Symbols = Array.Empty<BinanceSymbol>() };

        _mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(
                It.IsAny<bool?>(),
                It.IsAny<SymbolStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceExchangeInfo>(null,
                                                                null,
                                                                TimeSpan.Zero,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                null,
                                                                ResultDataSource.Server,
                                                                exchangeInfo,
                                                                null));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.GetSymbolFilterData(strategy));
    }
}
