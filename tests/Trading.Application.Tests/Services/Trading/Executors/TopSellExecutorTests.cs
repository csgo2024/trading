using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
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
    private readonly TopSellExecutor _executor;

    public TopSellExecutorTests()
    {
        _mockLogger = new Mock<ILogger<TopSellExecutor>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _executor = new TopSellExecutor(_mockLogger.Object, _mockStrategyRepository.Object);
    }

    [Fact]
    public async Task Execute_WhenOrderIdNotExist_ShouldResetStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy();
        SetupSuccessfulKlineResponse();
        SetupSuccessfulSymbolFilterResponse();
        SetupSuccessfulPlaceOrderResponse(12345L);
        SetupSuccessfulOrderStatusResponse(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

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
        SetupSuccessfulOrderStatusResponse(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Previous day's order not filled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_WithNewDay_ShouldResetStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy(orderPlacedTime: DateTime.UtcNow.AddDays(-1));
        SetupSuccessfulKlineResponse();
        SetupSuccessfulSymbolFilterResponse();
        SetupSuccessfulPlaceOrderResponse(12345L);
        SetupSuccessfulOrderStatusResponse(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

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
        SetupSuccessfulOrderStatusResponse(OrderStatus.Filled);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

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
        SetupSuccessfulOrderStatusResponse(status);
        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
        VerifyLogging(_mockLogger, $"[{strategy.AccountType}-{strategy.Symbol}] Order {status}. Will try to place new order.");
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

        SetupSuccessfulKlineResponse(100m);
        SetupSuccessfulSymbolFilterResponse();
        SetupSuccessfulOrderStatusResponse(OrderStatus.New);
        SetupSuccessfulCancelOrderResponse();
        SetupSuccessfulPlaceOrderResponse(54321L); // Add this line - new order ID different from canceled one

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(strategy.OrderId, 54321);
        Assert.NotNull(strategy.OrderPlacedTime);
        Assert.NotEqual(0, strategy.TargetPrice);
        Assert.NotEqual(0, strategy.Quantity);
        VerifyLogging(_mockLogger, $"[{strategy.AccountType}-{strategy.Symbol}] Previous day's order not filled, cancelling order before reset.");
        VerifyLogging(_mockLogger, $"[{strategy.AccountType}-{strategy.Symbol}] Successfully cancelled order");
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

        SetupSuccessfulKlineResponse(100m);
        SetupSuccessfulSymbolFilterResponse();
        SetupSuccessfulOrderStatusResponse(status);
        SetupSuccessfulCancelOrderResponse();
        SetupSuccessfulPlaceOrderResponse(54321L);

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        // Verify order cancellation
        VerifyLoggingSequence(_mockLogger, [
            $"[{strategy.AccountType}-{strategy.Symbol}] Previous day's order not filled, cancelling order before reset",
            $"[{strategy.AccountType}-{strategy.Symbol}] Successfully cancelled order"
        ]);

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

        SetupSuccessfulOrderStatusResponse(status);

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.Execute(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        // Verify order wasn't cancelled
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(12345L, strategy.OrderId); // Same order ID
        Assert.NotNull(strategy.OrderPlacedTime);

        // Verify no cancellation logs
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("initiating cancellation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task TryPlaceOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = CreateTestStrategy();
        var orderId = 12345L;
        SetupSuccessfulPlaceOrderResponse(orderId);

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
        SetupSuccessfulCancelOrderResponse();

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
        SetupSuccessfulKlineResponse(openPrice);
        SetupSuccessfulSymbolFilterResponse();

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
        var openPrice = 100m;
        SetupFailedKlineResponse(openPrice);

        // Act
        await _executor.ResetDailyStrategy(_mockAccountProcessor.Object, strategy, DateTime.UtcNow, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get daily open price")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static Strategy CreateTestStrategy(
        bool hasOpenOrder = false,
        long? orderId = null,
        DateTime? orderPlacedTime = null)
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
            OrderPlacedTime = orderPlacedTime
        };
    }

    private void SetupSuccessfulKlineResponse(decimal openPrice = 100m)
    {
        var kline = new Mock<IBinanceKline>();
        kline.Setup(x => x.OpenPrice).Returns(openPrice);

        _mockAccountProcessor
            .Setup(x => x.GetKlines(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<IEnumerable<IBinanceKline>>(
                null,
                null,
                null,
                0,
                null,
                0,
                null,
                null,
                null,
                null,
                ResultDataSource.Server,
                [kline.Object],
                null)
            );
    }
    private void SetupFailedKlineResponse(decimal openPrice = 100m)
    {
        var kline = new Mock<IBinanceKline>();
        kline.Setup(x => x.OpenPrice).Returns(openPrice);

        _mockAccountProcessor
            .Setup(x => x.GetKlines(
                It.IsAny<string>(),
                It.IsAny<KlineInterval>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<IEnumerable<IBinanceKline>>(
                null,
                null,
                null,
                0,
                null,
                0,
                null,
                null,
                null,
                null,
                ResultDataSource.Server,
                [kline.Object],
                new ServerError("Error"))
            );
    }

    private void SetupSuccessfulOrderStatusResponse(OrderStatus status)
    {
        _mockAccountProcessor
            .Setup(x => x.GetOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null,
                null,
                null,
                0,
                null,
                0,
                null,
                null,
                null,
                null,
                ResultDataSource.Server,
                new BinanceOrder { Status = status },
                null));
    }

    private void SetupSuccessfulPlaceOrderResponse(long orderId)
    {
        _mockAccountProcessor
            .Setup(x => x.PlaceShortOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null,
                null,
                null,
                0,
                null,
                0,
                null,
                null,
                null,
                null,
                ResultDataSource.Server,
                new BinanceOrder { Id = orderId },
                null)
            );
    }

    private void SetupSuccessfulCancelOrderResponse()
    {
        _mockAccountProcessor
            .Setup(x => x.CancelOrder(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCallResult<BinanceOrderBase>(
                null,
                null,
                null,
                0,
                null,
                0,
                null,
                null,
                null,
                null,
                ResultDataSource.Server,
                new BinanceOrder(),
                null
                ));
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

    private static void VerifyLogging(Mock<ILogger<TopSellExecutor>> logger, string expectedMessage)
    {
        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static void VerifyLoggingSequence(Mock<ILogger<TopSellExecutor>> logger, string[] expectedMessages)
    {
        foreach (var expectedMessage in expectedMessages)
        {
            logger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

}
