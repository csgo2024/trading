using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class TopSellExecutorTests
{
    private readonly Mock<ILogger<TopSellExecutor>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<IStrategyStateManager> _mockStrategyStateManager;
    private readonly TopSellExecutor _executor;

    public TopSellExecutorTests()
    {
        _mockLogger = new Mock<ILogger<TopSellExecutor>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockStrategyStateManager = new Mock<IStrategyStateManager>();
        _executor = new TopSellExecutor(_mockLogger.Object,
                                        _mockStrategyRepository.Object,
                                        _mockJavaScriptEvaluator.Object,
                                        _mockAccountProcessorFactory.Object,
                                        _mockStrategyStateManager.Object);
    }

    [Fact]
    public void StrategyType_ShouldEqualTo_TopSell()
    {
        var result = _executor.StrategyType;

        // Assert
        Assert.Equal(StrategyType.TopSell, result);
    }

    [Fact]
    public async Task Execute_WhenOrderIdNotExist_ShouldResetStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy();
        _mockAccountProcessor.SetupSuccessfulGetKlines();
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(12345L);
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.Equal(DateTime.UtcNow.Date, strategy.OrderPlacedTime?.Date);
        Assert.True(strategy.HasOpenOrder); // True since order should be placed
        Assert.Equal(strategy.OrderId, 12345L);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task Execute_WithSameDay_WhenOrderIdExists_ShouldNotResetStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy(true, 12345, DateTime.UtcNow);
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        _mockLogger.VerifyLoggingNever(LogLevel.Information, "Previous day's order not filled");
    }

    [Fact]
    public async Task Execute_WithNewDay_ShouldResetStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy(orderPlacedTime: DateTime.UtcNow.AddDays(-1));
        _mockAccountProcessor.SetupSuccessfulGetKlines();
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(12345L);
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.Equal(DateTime.UtcNow.Date, strategy.OrderPlacedTime?.Date);
        Assert.True(strategy.HasOpenOrder); // True since order should be placed
        Assert.NotNull(strategy.OrderId); //
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_WithOpenOrder_ShouldCheckOrderStatus()
    {
        // Arrange
        var strategy = CreateTestStrategy(hasOpenOrder: true, orderId: 12345);
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.Filled);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Equal(strategy.OrderId, 12345); // Order ID should remain the same
    }

    [Theory]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    [InlineData(OrderStatus.Rejected)]
    public async Task Execute_WithNonActiveOrderStatus_ShouldResetOrderStatus(OrderStatus status)
    {
        // Arrange
        var strategy = CreateTestStrategy(hasOpenOrder: true, orderId: 12345);
        _mockAccountProcessor.SetupSuccessfulGetOrder(status);
        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, $"[{strategy.AccountType}-{strategy.Symbol}] Order {status}. Will try to place new order.");
    }

    [Fact]
    public async Task Execute_WithPendingOrderFromPreviousDay_ShouldCancelOrder()
    {
        // Arrange
        var strategy = CreateTestStrategy(
            hasOpenOrder: true,
            orderId: 12345,
            orderPlacedTime: DateTime.UtcNow.AddDays(-1));
        strategy.OrderPlacedTime = DateTime.UtcNow.AddDays(-1);

        _mockAccountProcessor.SetupSuccessfulGetKlines(100m);
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);
        _mockAccountProcessor.SetupSuccessfulCancelOrder();
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(54321L); // Add this line - new order ID different from canceled one

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(strategy.OrderId, 54321);
        Assert.NotNull(strategy.OrderPlacedTime);
        Assert.NotEqual(0, strategy.TargetPrice);
        Assert.NotEqual(0, strategy.Quantity);
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, $"[{strategy.AccountType}-{strategy.Symbol}] Previous day's order not filled, cancelling order before reset.");
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, $"[{strategy.AccountType}-{strategy.Symbol}] Successfully cancelled order");
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    public async Task Execute_WithActiveOrder_WhenFromPreviousDay_ShouldCancelAndPlaceNewOrder(OrderStatus status)
    {
        // Arrange
        var strategy = CreateTestStrategy(
            hasOpenOrder: true,
            orderId: 12345);
        strategy.OrderPlacedTime = DateTime.UtcNow.AddDays(-1); // Set order placed time to previous day

        _mockAccountProcessor.SetupSuccessfulGetKlines(100m);
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulGetOrder(status);
        _mockAccountProcessor.SetupSuccessfulCancelOrder();
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(54321L);

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        // Verify order cancellation
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, $"[{strategy.AccountType}-{strategy.Symbol}] Previous day's order not filled, cancelling order before reset");
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, $"[{strategy.AccountType}-{strategy.Symbol}] Successfully cancelled order");

        // Verify strategy state after executed, should place new order.
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(strategy.OrderId, 54321);
        Assert.Equal(strategy.OrderPlacedTime!.Value.Date, DateTime.UtcNow.Date);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    public async Task Execute_WithActiveOrder_WhenFromSameDay_ShouldNotCancelOrder(OrderStatus status)
    {
        // Arrange
        var strategy = CreateTestStrategy(
            hasOpenOrder: true,
            orderId: 12345);
        strategy.OrderPlacedTime = DateTime.UtcNow; // Set order placed time to current day

        _mockAccountProcessor.SetupSuccessfulGetOrder(status);

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        // Verify order wasn't cancelled
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(12345L, strategy.OrderId); // Same order ID
        Assert.NotNull(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task TryPlaceOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy();
        var orderId = 12345L;
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(orderId);

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(orderId, strategy.OrderId);
        Assert.NotNull(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task CancelExistingOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy(hasOpenOrder: true, orderId: 12345);
        _mockAccountProcessor.SetupSuccessfulCancelOrder();

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task ResetDailyStrategy_WhenSuccessful_ShouldUpdateStrategyWithNewPrices()
    {
        // Arrange
        var strategy = CreateTestStrategy();
        var openPrice = 100m;
        _mockAccountProcessor.SetupSuccessfulGetKlines(openPrice);
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();

        // Act
        await _executor.ResetDailyStrategy(_mockAccountProcessor.Object, strategy, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.NotEqual(0, strategy.TargetPrice);
        Assert.True(strategy.TargetPrice > openPrice); // Short order target price must greater than today open price.
        Assert.NotEqual(0, strategy.Quantity);
        Assert.False(strategy.HasOpenOrder);
    }
    [Fact]
    public async Task ResetDailyStrategy_WhenFailed_ShouldLogError()
    {
        // Arrange
        var strategy = CreateTestStrategy();
        _mockAccountProcessor.SetupFailedGetKlines();

        // Act
        await _executor.ResetDailyStrategy(_mockAccountProcessor.Object, strategy, DateTime.UtcNow, CancellationToken.None);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, $"Failed to get daily open price");
    }

    private static Strategy CreateTestStrategy(bool hasOpenOrder = false, long? orderId = null, DateTime? orderPlacedTime = null)
    {
        return new Strategy
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            AccountType = AccountType.Future,
            StrategyType = StrategyType.TopSell,
            Amount = 1000,
            Volatility = 0.01m,
            HasOpenOrder = hasOpenOrder,
            OrderId = orderId,
            RequireReset = false,
            OrderPlacedTime = orderPlacedTime
        };
    }
}
