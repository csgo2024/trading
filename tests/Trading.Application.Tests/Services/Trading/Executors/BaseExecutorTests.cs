using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.JavaScript;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class TestExecutor : BaseExecutor
{
    public TestExecutor(ILogger logger,
                        IStrategyRepository strategyRepository,
                        JavaScriptEvaluator javaScriptEvaluator)
        : base(logger, strategyRepository, javaScriptEvaluator)
    {
    }

    public override async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        await base.ExecuteAsync(accountProcessor, strategy, ct);
    }

    public override Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class BaseExecutorTests
{
    private readonly Mock<ILogger<TestExecutor>> _mockLogger;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly TestExecutor _executor;
    private readonly CancellationToken _ct;

    public BaseExecutorTests()
    {
        _mockLogger = new Mock<ILogger<TestExecutor>>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _executor = new TestExecutor(_mockLogger.Object, _mockStrategyRepository.Object, _mockJavaScriptEvaluator.Object);
        _ct = CancellationToken.None;
    }
    [Fact]
    public async Task Execute_ShouldCheckOrderStatusAndUpdateStrategy()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };
        SetupSuccessfulPlaceOrderResponse(12345L);
        SetupOrderStatusResponse(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.True(strategy.HasOpenOrder); // True since order status is new.
        Assert.NotNull(strategy.OrderId);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelExistingOrder_WithNoOrderId_ShouldReturnImmediately()
    {
        // Arrange
        var strategy = new Strategy { OrderId = null };

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.CancelOrder(
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task CancelExistingOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        SetupSuccessfulCancelOrderResponse();

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task CancelExistingOrder_WhenFailed_ShouldLogError()
    {
        // Arrange
        var strategy = new Strategy { OrderId = 12345 };
        var error = "Cancel order failed";

        SetupFailedCancelOrderResponse(error);

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        VerifyErrorLogging($"Failed to cancel order. Error: {error}");
    }

    [Fact]
    public async Task CheckOrderStatus_WithNoOrderId_ShouldResetHasOpenOrder()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = null,
            HasOpenOrder = true
        };

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
    }

    [Theory]
    [InlineData(OrderStatus.Filled)]
    public async Task CheckOrderStatus_WithFilledOrder_ShouldUpdateStrategy(OrderStatus status)
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        SetupOrderStatusResponse(status);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Equal(strategy.OrderId, 12345); // Order ID should remain the same
        Assert.NotNull(strategy.OrderPlacedTime);
    }

    [Theory]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    [InlineData(OrderStatus.Rejected)]
    public async Task CheckOrderStatus_WithFailedOrder_ShouldResetOrderStatus(OrderStatus status)
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        SetupOrderStatusResponse(status);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    public async Task CheckOrderStatus_WithActiveOrder_FromSameDay_ShouldNotCancelOrder(OrderStatus status)
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        SetupOrderStatusResponse(status);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.CancelOrder(
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }
    [Fact]
    public async Task CheckOrderStatus_WithFailed_ShouldLogError()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        var error = "Failed to get order status";
        SetupFailedGetOrderResponse(error);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        VerifyErrorLogging($"Failed to check order status, Error: {error}");
    }

    [Fact]
    public async Task TryPlaceOrder_WhenOrderIdExists_ShouldDoNothing()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            OrderId = 12345L,
            OrderPlacedTime = DateTime.UtcNow,
            TargetPrice = 50000m
        };

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryPlaceOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m
        };

        SetupSuccessfulPlaceOrderResponse(12345L);

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(12345L, strategy.OrderId);
        Assert.NotNull(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task TryPlaceOrder_WhenFailedWithRetries_ShouldLogWarningsAndError()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m
        };

        SetupFailedPlaceOrderResponse("Insufficient balance");

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrying")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    private void SetupSuccessfulCancelOrderResponse()
    {
        _mockAccountProcessor
            .Setup(x => x.CancelOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null, null, null, 0, null, 0, null, null, null, null,
                ResultDataSource.Server, new BinanceOrder(), null));
    }

    private void SetupFailedCancelOrderResponse(string error)
    {
        _mockAccountProcessor
            .Setup(x => x.CancelOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null, null, null, 0, null, 0, null, null, null, null,
                ResultDataSource.Server, null, new ServerError(0, error)));
    }

    private void SetupFailedGetOrderResponse(string error)
    {
        _mockAccountProcessor
            .Setup(x => x.GetOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null, null, null, 0, null, 0, null, null, null, null,
                ResultDataSource.Server, null, new ServerError(0, error)));
    }
    private void SetupOrderStatusResponse(OrderStatus status)
    {
        _mockAccountProcessor
            .Setup(x => x.GetOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null, null, null, 0, null, 0, null, null, null, null,
                ResultDataSource.Server, new BinanceOrder { Status = status }, null));
    }

    private void SetupSuccessfulPlaceOrderResponse(long orderId)
    {
        _mockAccountProcessor
            .Setup(x => x.PlaceLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null, null, null, 0, null, 0, null, null, null, null,
                ResultDataSource.Server, new BinanceOrder { Id = orderId }, null));
    }

    private void SetupFailedPlaceOrderResponse(string error)
    {
        _mockAccountProcessor
            .Setup(x => x.PlaceLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null, null, null, 0, null, 0, null, null, null, null,
                ResultDataSource.Server, null, new ServerError(0, error)));
    }

    private void VerifyErrorLogging(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
