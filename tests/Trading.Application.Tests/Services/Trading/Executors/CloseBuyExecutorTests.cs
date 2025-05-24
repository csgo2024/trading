using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class CloseBuyExecutorTests
{
    private readonly Mock<ILogger<CloseBuyExecutor>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<IStrategyStateManager> _mockStrategyStateManager;
    private readonly CloseBuyExecutor _executor;
    private readonly CancellationToken _ct;

    public CloseBuyExecutorTests()
    {
        _mockLogger = new Mock<ILogger<CloseBuyExecutor>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockStrategyStateManager = new Mock<IStrategyStateManager>();
        _executor = new CloseBuyExecutor(
            _mockLogger.Object,
            _mockAccountProcessorFactory.Object,
            _mockStrategyRepository.Object,
            _mockJavaScriptEvaluator.Object,
            _mockStrategyStateManager.Object
        );
        _ct = CancellationToken.None;
    }

    private static KlineClosedEvent SetupKlineCloseEvent()
    {
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var notification = new KlineClosedEvent(symbol, interval, kline);
        return notification;
    }

    [Fact]
    public async Task Handle_WithNoStrategiesFound_ShouldNotProcessAnything()
    {
        // Arrange
        var notification = SetupKlineCloseEvent();

        _mockStrategyRepository.Setup(x => x.FindActiveStrategyByType(
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([]);

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockAccountProcessorFactory.Verify(x => x.GetAccountProcessor(It.IsAny<AccountType>()), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidStrategy_ShouldProcessAndUpdateStrategy()
    {
        // Arrange
        var notification = SetupKlineCloseEvent();

        var strategy = new Strategy
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Volatility = 0.01m,
            Amount = 1000,
            HasOpenOrder = false,
            StrategyType = StrategyType.CloseBuy,
            Interval = "1d"
        };

        _mockStrategyStateManager
            .Setup(x => x.GetState(It.IsAny<StrategyType>()))
            .Returns(new Dictionary<string, Strategy>
            {
                { strategy.Id, strategy }
            });

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);

        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulPlaceLongOrderAsync(12345L);
        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockStrategyRepository.Verify(x => x.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<Strategy>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        // CloseBuy entry price should be lower than close price
        Assert.True(strategy.TargetPrice < 41000m);
    }

    [Fact]
    public async Task Handle_WithNullAccountProcessor_ShouldSkipProcessing()
    {
        // Arrange
        var notification = SetupKlineCloseEvent();

        var strategy = new Strategy
        {
            Id = "test-id",
            StrategyType = StrategyType.CloseBuy,
        };

        _mockStrategyRepository.Setup(x => x.FindActiveStrategyByType(
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([strategy]);

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
        var notification = SetupKlineCloseEvent();

        var strategy = new Strategy
        {
            Id = "test-id",
            HasOpenOrder = true,
            StrategyType = StrategyType.CloseBuy,
            OrderId = 12345L
        };

        _mockStrategyRepository.Setup(x => x.FindActiveStrategyByType(
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([strategy]);

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);

        _mockAccountProcessor.SetupSuccessfulSymbolFilter();

        // Act
        await _executor.Handle(notification, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceLongOrderAsync(
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            It.IsAny<TimeInForce>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }
}
