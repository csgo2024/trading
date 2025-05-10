using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Spot;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class CloseSellExecutorTests
{
    private readonly Mock<ILogger<CloseSellExecutor>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly CloseSellExecutor _executor;
    private readonly CancellationToken _ct;

    public CloseSellExecutorTests()
    {
        _mockLogger = new Mock<ILogger<CloseSellExecutor>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _executor = new CloseSellExecutor(
            _mockLogger.Object,
            _mockAccountProcessorFactory.Object,
            _mockStrategyRepository.Object
        );
        _ct = CancellationToken.None;
    }

    [Fact]
    public async Task Handle_WithNoStrategiesFound_ShouldNotProcessAnything()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var notification = new KlineClosedEvent(symbol, interval, kline);

        _mockStrategyRepository.Setup(x => x.Find(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new List<Strategy>());

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockAccountProcessorFactory.Verify(x => x.GetAccountProcessor(It.IsAny<AccountType>()), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<Strategy>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task Handle_WithSpotAccountType_ShouldSkipProcessing()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var notification = new KlineClosedEvent(symbol, interval, kline);

        var strategy = new Strategy
        {
            AccountType = AccountType.Spot
        };

        _mockStrategyRepository.Setup(x => x.Find(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new List<Strategy> { strategy });

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockAccountProcessorFactory.Verify(x => x.GetAccountProcessor(It.IsAny<AccountType>()), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<Strategy>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidStrategy_ShouldProcessAndUpdateStrategy()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var notification = new KlineClosedEvent(symbol, interval, kline);

        var strategy = new Strategy
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Volatility = 0.01m,
            Amount = 1000,
            HasOpenOrder = false,
            AccountType = AccountType.Future
        };

        _mockStrategyRepository.Setup(x => x.Find(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new List<Strategy> { strategy });

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);

        SetupSuccessfulSymbolFilterResponse();

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockStrategyRepository.Verify(x => x.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<Strategy>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // CloseSell entry price should be higher than close price
        Assert.True(strategy.TargetPrice > 41000m);
    }

    [Fact]
    public async Task Handle_WithNullAccountProcessor_ShouldSkipProcessing()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var notification = new KlineClosedEvent(symbol, interval, kline);

        var strategy = new Strategy
        {
            Id = "test-id",
            AccountType = AccountType.Future
        };

        _mockStrategyRepository.Setup(x => x.Find(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new List<Strategy> { strategy });

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(null as IAccountProcessor);

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.GetSymbolFilterData(
            It.IsAny<Strategy>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<Strategy>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingOrder_ShouldNotPlaceNewOrder()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var notification = new KlineClosedEvent(symbol, interval, kline);

        var strategy = new Strategy
        {
            Id = "test-id",
            HasOpenOrder = true,
            AccountType = AccountType.Future
        };

        _mockStrategyRepository.Setup(x => x.Find(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([strategy]);

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);

        SetupSuccessfulSymbolFilterResponse();

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceShortOrderAsync(
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            It.IsAny<TimeInForce>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    private void SetupSuccessfulSymbolFilterResponse()
    {
        _mockAccountProcessor
            .Setup(x => x.GetSymbolFilterData(
                It.IsAny<Strategy>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new BinanceSymbolPriceFilter()
            {
                TickSize = 0.001m,
                MaxPrice = decimal.MaxValue,
                MinPrice = decimal.MinValue,
            }, new BinanceSymbolLotSizeFilter()
            {
                StepSize = 0.01m,
                MinQuantity = decimal.MinValue,
                MaxQuantity = decimal.MaxValue,
            }));
    }
}
