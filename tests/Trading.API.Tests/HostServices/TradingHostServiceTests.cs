using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.HostServices;

public class TradingHostServiceTests : IDisposable
{
    private readonly Mock<ILogger<TradingHostService>> _loggerMock;
    private readonly Mock<StrategyDispatchService> _strategyDispatchServiceMock;
    private readonly CancellationTokenSource _cts;
    private readonly TestTradingHostService _service;

    public TradingHostServiceTests()
    {
        _loggerMock = new Mock<ILogger<TradingHostService>>();
        _strategyDispatchServiceMock = new Mock<StrategyDispatchService>(
            Mock.Of<ILogger<StrategyDispatchService>>(),
            Mock.Of<IAccountProcessorFactory>(),
            Mock.Of<IExecutorFactory>(),
            Mock.Of<IBackgroundTaskManager>(),
            Mock.Of<IStrategyRepository>());
        _cts = new CancellationTokenSource();

        _service = new TestTradingHostService(
            _loggerMock.Object,
            _strategyDispatchServiceMock.Object);
    }

    private sealed class TestTradingHostService : TradingHostService
    {
        private int _delayCallCount;

        public TestTradingHostService(
            ILogger<TradingHostService> logger,
            StrategyDispatchService strategyDispatchService)
            : base(logger, strategyDispatchService)
        {
        }

        public override Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            _delayCallCount++;

            if (_delayCallCount >= 2)
            {
                throw new OperationCanceledException();
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteStrategyService()
    {
        // Arrange
        _strategyDispatchServiceMock
            .Setup(x => x.DispatchAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        try
        {
            await _service.StartAsync(_cts.Token);
        }
        catch (Exception)
        {
            // ignored
        }

        // Assert
        _strategyDispatchServiceMock.Verify(
            x => x.DispatchAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(1));
    }

    [Fact]
    public async Task ExecuteAsync_WhenStrategyServiceThrowsException_ShouldLogErrorAndContinue()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        _strategyDispatchServiceMock
            .Setup(x => x.DispatchAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        try
        {
            await _service.StartAsync(_cts.Token);
        }
        catch (Exception)
        {
            // ignored
        }
        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error initializing trading service")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));

        _strategyDispatchServiceMock.Verify(
            x => x.DispatchAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectCancellation()
    {
        // Arrange
        _cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await _service.StartAsync(_cts.Token);
        }
        catch (Exception)
        {
            // ignored
        }

        // Assert
        _strategyDispatchServiceMock.Verify(
            x => x.DispatchAsync(It.IsAny<CancellationToken>()),
            Times.AtMost(2));
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
