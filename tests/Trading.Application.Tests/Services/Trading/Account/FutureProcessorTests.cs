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

namespace Trading.Application.Tests.Services.Trading.Account;

public class FutureProcessorTests
{
    private readonly BinanceRestClientUsdFuturesApiWrapper _binanceClient;
    private readonly Mock<IBinanceRestClientUsdFuturesApiAccount> _mockAccount;
    private readonly Mock<IBinanceRestClientUsdFuturesApiTrading> _mockTrading;
    private readonly Mock<IBinanceRestClientUsdFuturesApiExchangeData> _mockExchangeData;
    private readonly FutureProcessor _processor;

    public FutureProcessorTests()
    {
        _mockAccount = new Mock<IBinanceRestClientUsdFuturesApiAccount>();
        _mockTrading = new Mock<IBinanceRestClientUsdFuturesApiTrading>();
        _mockExchangeData = new Mock<IBinanceRestClientUsdFuturesApiExchangeData>();

        _binanceClient = new BinanceRestClientUsdFuturesApiWrapper(_mockAccount.Object, _mockExchangeData.Object, _mockTrading.Object);
        _processor = new FutureProcessor(_binanceClient);
    }

    [Fact]
    public async Task GetOrder_WhenSuccessful_ShouldReturnOrder()
    {
        // Arrange
        var expectedOrder = new BinanceUsdFuturesOrder
        {
            Id = 12345,
            Status = OrderStatus.Filled
        };

        _mockTrading
            .Setup(x => x.GetOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(
                null, null, TimeSpan.Zero, null, null, null, null, null, null, null,
                ResultDataSource.Server, expectedOrder, null));

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
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(expectedError));

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
            .ReturnsAsync(new WebCallResult<IEnumerable<IBinanceKline>>(
                null, null, TimeSpan.Zero, null, null, null, null, null, null, null,
                ResultDataSource.Server, expectedKlines, null));

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
        var expectedOrder = new BinanceUsdFuturesOrder
        {
            Id = 12345,
            Status = OrderStatus.New
        };

        _mockTrading
            .Setup(m => m.PlaceOrderAsync(
                It.IsAny<string>(),              // 忽略 symbol
                It.IsAny<OrderSide>(),           // 忽略 side
                It.IsAny<FuturesOrderType>(),    // 忽略 type
                It.IsAny<decimal?>(),            // 忽略 quantity
                It.IsAny<decimal?>(),            // 忽略 price
                It.IsAny<PositionSide?>(),       // 忽略 positionSide
                It.IsAny<TimeInForce?>(),        // 忽略 timeInForce
                It.IsAny<bool?>(),               // 忽略 reduceOnly
                It.IsAny<string?>(),             // 忽略 newClientOrderId
                It.IsAny<decimal?>(),            // 忽略 stopPrice
                It.IsAny<decimal?>(),            // 忽略 activationPrice
                It.IsAny<decimal?>(),            // 忽略 callbackRate
                It.IsAny<WorkingType?>(),        // 忽略 workingType
                It.IsAny<bool?>(),               // 忽略 closePosition
                It.IsAny<OrderResponseType?>(),  // 忽略 orderResponseType
                It.IsAny<bool?>(),               // 忽略 priceProtect
                It.IsAny<PriceMatch?>(),         // 忽略 priceMatch
                It.IsAny<SelfTradePreventionMode?>(), // 忽略 selfTradePreventionMode
                It.IsAny<DateTime?>(),           // 忽略 goodTillDate
                It.IsAny<int?>(),                // 忽略 receiveWindow
                It.IsAny<CancellationToken>()    // 忽略 ct
            )).ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(
                null, null, TimeSpan.Zero, null, null, null, null, null, null, null,
                ResultDataSource.Server, expectedOrder, null));

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

        // Verify correct parameters were passed
        _mockTrading.Verify(x => x.PlaceOrderAsync(
                "BTCUSDT",                      // symbol
                OrderSide.Buy,                  // side
                FuturesOrderType.Limit,         // type
                1.0m,                          // quantity
                50000m,                        // price
                PositionSide.Long,             // positionSide
                TimeInForce.GoodTillCanceled,  // timeInForce
                null,                          // reduceOnly
                null,                          // newClientOrderId
                null,                          // stopPrice
                null,                          // activationPrice
                null,                          // callbackRate
                null,                          // workingType
                null,                          // closePosition
                null,                          // orderResponseType
                null,                          // priceProtect
                null,                          // priceMatch
                null,                          // selfTradePreventionMode
                null,                          // goodTillDate
                null,                          // receiveWindow
                It.IsAny<CancellationToken>()), // ct
          Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WhenSuccessful_ShouldReturnCanceledOrder()
    {
        // Arrange
        var expectedOrder = new BinanceUsdFuturesOrder
        {
            Id = 12345,
            Status = OrderStatus.Canceled
        };

        _mockTrading
            .Setup(x => x.CancelOrderAsync(
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceUsdFuturesOrder>(
                null, null, TimeSpan.Zero, null, null, null, null, null, null, null,
                ResultDataSource.Server, expectedOrder, null));

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
        var strategy = new Strategy { Symbol = "BTCUSDT", AccountType = Domain.Entities.AccountType.Future };
        var expectedPriceFilter = new BinanceSymbolPriceFilter
        {
            TickSize = 0.01m,
            MinPrice = 0.01m,
            MaxPrice = 100000m
        };
        var expectedLotSizeFilter = new BinanceSymbolLotSizeFilter
        {
            StepSize = 0.001m,
            MinQuantity = 0.001m,
            MaxQuantity = 1000m
        };

        var exchangeInfo = new BinanceFuturesUsdtExchangeInfo
        {
            Symbols = new[]
            {
                new BinanceFuturesUsdtSymbol
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
            .Setup(x => x.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceFuturesUsdtExchangeInfo>(
                null, null, TimeSpan.Zero, null, null, null, null, null, null, null,
                ResultDataSource.Server, exchangeInfo, null));

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
        var strategy = new Strategy { Symbol = "UNKNOWN", AccountType = Domain.Entities.AccountType.Future };
        var exchangeInfo = new BinanceFuturesUsdtExchangeInfo
        {
            Symbols = Array.Empty<BinanceFuturesUsdtSymbol>()
        };

        _mockExchangeData
            .Setup(x => x.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceFuturesUsdtExchangeInfo>(
                null, null, TimeSpan.Zero, null, null, null, null, null, null, null,
                ResultDataSource.Server, exchangeInfo, null));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.GetSymbolFilterData(strategy));
        Assert.Contains($"[{strategy.AccountType}-{strategy.Symbol}]", exception.Message);
    }
}
