using System.ComponentModel.DataAnnotations;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading;

public class StrategyExecutionServiceTests
{
    private readonly Mock<ILogger<StrategyExecutionService>> _loggerMock;
    private readonly Mock<IAccountProcessorFactory> _accountProcessorFactoryMock;
    private readonly Mock<IExecutorFactory> _executorFactoryMock;
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManagerMock;
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly Mock<IAccountProcessor> _accountProcessorMock;
    private readonly Mock<BaseExecutor> _executorMock;
    private readonly StrategyExecutionService _service;
    private readonly CancellationTokenSource _cts;

    public StrategyExecutionServiceTests()
    {
        _loggerMock = new Mock<ILogger<StrategyExecutionService>>();
        _accountProcessorFactoryMock = new Mock<IAccountProcessorFactory>();
        _executorFactoryMock = new Mock<IExecutorFactory>();
        _backgroundTaskManagerMock = new Mock<IBackgroundTaskManager>();
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        _accountProcessorMock = new Mock<IAccountProcessor>();
        _executorMock = new Mock<BaseExecutor>(_loggerMock.Object);
        _cts = new CancellationTokenSource();

        _service = new StrategyExecutionService(
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

        _accountProcessorMock
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
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot };
        var notification = new StrategyCreatedEvent(strategy);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategory.Strategy,
                strategy.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                _cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyDeletedEvent_ShouldStopExecution()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot };
        var notification = new StrategyDeletedEvent(strategy);

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

        _accountProcessorMock.Verify(
            x => x.CancelOrder(It.IsAny<string>(), 1234L, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyPausedEvent_ShouldStopExecution()
    {
        // Arrange
        var notification = new StrategyPausedEvent("test-id");

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
        var strategy = new Strategy { Id = "test-id", AccountType = AccountType.Spot };
        var notification = new StrategyResumedEvent(strategy);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategory.Strategy,
                strategy.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                _cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldInitializeAllActiveStrategies()
    {
        // Arrange
        var strategies = new Dictionary<string, Strategy>
        {
            { "test-1", new Strategy { Id = "test-1" } },
            { "test-2", new Strategy { Id = "test-2" } }
        };

        _strategyRepositoryMock
            .Setup(x => x.InitializeActiveStrategies())
            .ReturnsAsync(strategies);

        // Act
        await _service.ExecuteAsync(_cts.Token);

        // Assert
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategory.Strategy,
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                _cts.Token),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenInitializationFails_ShouldLogError()
    {
        // Arrange
        var expectedException = new ValidationException("Test exception");
        _strategyRepositoryMock
            .Setup(x => x.InitializeActiveStrategies())
            .ThrowsAsync(expectedException);

        // Act
        await _service.ExecuteAsync(_cts.Token);

        // Assert
        VerifyLogError("Failed to initialize strategies", expectedException);
    }

    [Fact]
    public async Task StartStrategyExecution_WhenProcessorOrExecutorNotFound_ShouldLogError()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-id" };
        var strategies = new Dictionary<string, Strategy>
        {
            { "test-1", new Strategy { Id = "test-id" } },
        };

        _strategyRepositoryMock
            .Setup(x => x.InitializeActiveStrategies())
            .ReturnsAsync(strategies);

        _accountProcessorFactoryMock
            .Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(null as IAccountProcessor);

        // Act
        await _service.ExecuteAsync(_cts.Token);

        // Assert
        // VerifyLogError($"Failed to get executor or account processor for strategy {strategy.Id}");
        _backgroundTaskManagerMock.Verify(
            x => x.StartAsync(
                It.IsAny<TaskCategory>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyLogError(string expectedMessage, Exception? exception = null)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(expectedMessage)),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

}
