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
    private readonly BinanceRestClientSpotApiWrapper _spotApiRestClient;
    private readonly Mock<IBinanceRestClientSpotApiAccount> _mockAccount;
    private readonly Mock<IBinanceRestClientSpotApiTrading> _mockTrading;
    private readonly Mock<IBinanceRestClientSpotApiExchangeData> _mockExchangeData;
    private readonly SpotProcessor _processor;

    public SpotProcessorTests()
    {
        _mockAccount = new Mock<IBinanceRestClientSpotApiAccount>();
        _mockTrading = new Mock<IBinanceRestClientSpotApiTrading>();
        _mockExchangeData = new Mock<IBinanceRestClientSpotApiExchangeData>();

        _spotApiRestClient = new BinanceRestClientSpotApiWrapper(_mockAccount.Object, _mockExchangeData.Object, _mockTrading.Object);
        _processor = new SpotProcessor(_spotApiRestClient);
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
            It.IsAny<CancellationToken>()
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
        var result = await _processor.PlaceLongOrderAsync(
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
                It.IsAny<string>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancelRestriction?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()
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
        var strategy = new Strategy { Symbol = "BTCUSDT", AccountType = AccountType.Spot };
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
        var strategy = new Strategy { Symbol = "UNKNOWN", AccountType = AccountType.Spot };
        var exchangeInfo = new BinanceExchangeInfo { Symbols = [] };

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
