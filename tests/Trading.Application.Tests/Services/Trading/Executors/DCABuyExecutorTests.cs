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

public class DCABuyExecutorTests
{
    private readonly Mock<ILogger<DCABuyExecutor>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<IStrategyStateManager> _mockStrategyStateManager;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly DCABuyExecutor _executor;
    private readonly CancellationToken _ct;

    public DCABuyExecutorTests()
    {
        _mockLogger = new Mock<ILogger<DCABuyExecutor>>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockStrategyStateManager = new Mock<IStrategyStateManager>();
        _executor = new DCABuyExecutor(
            _mockLogger.Object,
            _mockStrategyRepository.Object,
            _mockJavaScriptEvaluator.Object,
            _mockAccountProcessorFactory.Object,
            _mockStrategyStateManager.Object
        );
        _ct = CancellationToken.None;
    }
    [Fact]
    public void StrategyType_ShouldEqualTo_DCA()
    {
        var result = _executor.StrategyType;

        // Assert
        Assert.Equal(StrategyType.DCA, result);
    }
    [Fact]
    public async Task Execute_ShouldThrowNotImplementedException()
    {
        // Arrange
        var strategy = CreateTestStrategy(true, 12345, DateTime.UtcNow);

        // Act
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None));
    }
    private static Strategy CreateTestStrategy(bool hasOpenOrder = false, long? orderId = null, DateTime? orderPlacedTime = null)
    {
        return new Strategy
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            AccountType = AccountType.Spot,
            StrategyType = StrategyType.BottomBuy,
            Amount = 1000,
            Volatility = 0.01m,
            HasOpenOrder = hasOpenOrder,
            OrderId = orderId,
            OrderPlacedTime = orderPlacedTime ?? DateTime.UtcNow
        };
    }
}
