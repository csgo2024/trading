using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading;

public class StrategyDispatchServiceTests
{
    private readonly Mock<ILogger<StrategyDispatchService>> _loggerMock;
    private readonly Mock<IAccountProcessorFactory> _accountProcessorFactoryMock;
    private readonly Mock<IExecutorFactory> _executorFactoryMock;
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManagerMock;
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly Mock<IAccountProcessor> _accountProcessorMock;
    private readonly Mock<BaseExecutor> _executorMock;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<IStrategyStateManager> _mockStrategyStateManager;

    private readonly StrategyDispatchService _service;
    private readonly CancellationTokenSource _cts;

    public StrategyDispatchServiceTests()
    {
        _loggerMock = new Mock<ILogger<StrategyDispatchService>>();
        _accountProcessorFactoryMock = new Mock<IAccountProcessorFactory>();
        _executorFactoryMock = new Mock<IExecutorFactory>();
        _backgroundTaskManagerMock = new Mock<IBackgroundTaskManager>();
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        _accountProcessorMock = new Mock<IAccountProcessor>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockStrategyStateManager = new Mock<IStrategyStateManager>();
        _executorMock = new Mock<BaseExecutor>(_loggerMock.Object,
                                               _strategyRepositoryMock.Object,
                                               _mockJavaScriptEvaluator.Object,
                                               _accountProcessorFactoryMock.Object,
                                               _mockStrategyStateManager.Object);
        _cts = new CancellationTokenSource();

        _service = new StrategyDispatchService(
            _loggerMock.Object,
            _accountProcessorFactoryMock.Object,
            _executorFactoryMock.Object,
            _backgroundTaskManagerMock.Object,
            _strategyRepositoryMock.Object);

        SetupDefaults();
    }

    private void SetupDefaults()
    {
        _accountProcessorFactoryMock
            .Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_accountProcessorMock.Object);

        _accountProcessorMock.SetupSuccessfulCancelOrder();

        _executorFactoryMock
            .Setup(x => x.GetExecutor(It.IsAny<StrategyType>()))
            .Returns(_executorMock.Object);

        _backgroundTaskManagerMock
            .Setup(x => x.StartAsync(
                It.IsAny<TaskCategory>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_StrategyCreatedEvent_ShouldStartExecution()
    {
        // Arrange
        var strategy = new Strategy
        {
            Id = "test-id",
            StrategyType = StrategyType.CloseSell,
            AccountType = AccountType.Spot
        };
        var notification = new StrategyCreatedEvent(strategy);

        _executorMock
            .Setup(x => x.GetMonitoringStrategy())
            .Returns(new Dictionary<string, Strategy> { { strategy.Id, strategy } });

        _executorMock
            .Setup(x => x.LoadActiveStratey(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategory.Strategy,
                strategy.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                _cts.Token),
            Times.Exactly(5));
    }

    [Fact]
    public async Task Handle_StrategyDeletedEvent_ShouldStopExecution()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot };
        var notification = new StrategyDeletedEvent(strategy);
        _strategyRepositoryMock.Setup(x => x.FindActiveStrategyByType(
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([]);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StopAsync(TaskCategory.Strategy, "test-id"),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyDeletedEvent_ShouldCancelOrder_WhenHasOpenOrder()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot, OrderId = 1234L };
        var notification = new StrategyDeletedEvent(strategy);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StopAsync(TaskCategory.Strategy, "test-id"),
            Times.Once);

        _executorMock.Verify(
            x => x.CancelExistingOrder(
                It.IsAny<IAccountProcessor>(),
                It.Is<Strategy>(y => y.OrderId == 1234L),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyPausedEvent_ShouldStopExecution()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot, OrderId = 1234L };
        var notification = new StrategyPausedEvent(strategy);

        _strategyRepositoryMock.Setup(x => x.FindActiveStrategyByType(
            It.IsAny<StrategyType>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([]);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StopAsync(TaskCategory.Strategy, "test-id"),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyResumedEvent_ShouldRestartExecution()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot, StrategyType = StrategyType.CloseBuy };
        var notification = new StrategyResumedEvent(strategy);

        _executorMock
            .Setup(x => x.GetMonitoringStrategy())
            .Returns(new Dictionary<string, Strategy> { { strategy.Id, strategy } });

        _executorMock
            .Setup(x => x.LoadActiveStratey(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategory.Strategy,
                strategy.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                _cts.Token),
            Times.Exactly(5));
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleStrategies_ShouldStartAllTasks()
    {
        // Arrange
        var strategies = new Dictionary<string, Strategy>
        {
            { "test-1", new Strategy { Id = "test-1", StrategyType = StrategyType.TopSell } },
            { "test-2", new Strategy { Id = "test-2", StrategyType = StrategyType.BottomBuy } }
        };

        _executorMock
            .Setup(x => x.GetMonitoringStrategy())
            .Returns(strategies);

        _executorMock
            .Setup(x => x.LoadActiveStratey(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DispatchAsync(_cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategory.Strategy,
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(strategies.Count * 5));
    }

    [Fact]
    public async Task DispatchAsync_WithEmptyStrategyList_ShouldNotStartAnyTasks()
    {
        // Arrange
        _executorMock
            .Setup(x => x.GetMonitoringStrategy())
            .Returns(new Dictionary<string, Strategy>());

        // Act
        await _service.DispatchAsync(_cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                It.IsAny<TaskCategory>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
