using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Common;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.HostServices;

public class AlertHostServiceTests : IDisposable
{
    private readonly Mock<ILogger<AlertHostService>> _loggerMock;
    private readonly Mock<IKlineStreamManager> _klineStreamManagerMock;
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly CancellationTokenSource _cts;
    private readonly TestAlertHostService _service;

    public AlertHostServiceTests()
    {
        _loggerMock = new Mock<ILogger<AlertHostService>>();
        _klineStreamManagerMock = new Mock<IKlineStreamManager>();
        _alertRepositoryMock = new Mock<IAlertRepository>();
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        var backgroundTaskManagerMock = new Mock<IBackgroundTaskManager>();
        _cts = new CancellationTokenSource();

        var notificationLoggerMock = new Mock<ILogger<AlertNotificationService>>();
        var jsEvaluatorLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();
        var jsEvaluator = new JavaScriptEvaluator(jsEvaluatorLoggerMock.Object);

        var alertNotificationService = new AlertNotificationService(
            notificationLoggerMock.Object,
            _alertRepositoryMock.Object,
            jsEvaluator,
            backgroundTaskManagerMock.Object);

        _service = new TestAlertHostService(
            _loggerMock.Object,
            _klineStreamManagerMock.Object,
            alertNotificationService,
            _alertRepositoryMock.Object,
            _strategyRepositoryMock.Object);

        SetupDefaults();
    }

    private sealed class TestAlertHostService : AlertHostService
    {
        private int _delayCallCount;

        public TestAlertHostService(
            ILogger<AlertHostService> logger,
            IKlineStreamManager klineStreamManager,
            AlertNotificationService alertNotificationService,
            IAlertRepository alertRepository,
            IStrategyRepository strategyRepository)
            : base(logger, klineStreamManager, alertNotificationService, strategyRepository, alertRepository)
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

    private void SetupDefaults()
    {
        _klineStreamManagerMock
            .Setup(x => x.NeedsReconnection())
            .Returns(false);

        _klineStreamManagerMock
            .Setup(x => x.SubscribeSymbols(
                It.IsAny<HashSet<string>>(),
                It.IsAny<HashSet<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _alertRepositoryMock
            .Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActiveAlerts_ShouldNotSubscribe()
    {
        // Arrange
        _alertRepositoryMock
            .Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());

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
        _klineStreamManagerMock.Verify(
            x => x.SubscribeSymbols(
                It.IsAny<HashSet<string>>(),
                It.IsAny<HashSet<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithActiveAlerts_ShouldSubscribe()
    {
        // Arrange
        var alerts = new List<Alert>
        {
            new() { Symbol = "BTCUSDT", Interval = "5m" },
            new() { Symbol = "ETHUSDT", Interval = "15m" }
        };

        var strategy = new Strategy { Id = "123", Symbol = "DOGEUSDT", Interval = "4h", StrategyType = StrategyType.CloseBuy };

        _strategyRepositoryMock
            .Setup(x => x.FindActiveStrategies())
            .ReturnsAsync(new Dictionary<string, Strategy> { { "123", strategy } });

        _alertRepositoryMock
            .Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

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
        _klineStreamManagerMock.Verify(
            x => x.SubscribeSymbols(
                It.Is<HashSet<string>>(s => s.SetEquals(new[] { "BTCUSDT", "ETHUSDT", "DOGEUSDT" })),
                It.Is<HashSet<string>>(i => i.SetEquals(new[] { "5m", "15m", "4h" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNeedsReconnection_ShouldResubscribe()
    {
        // Arrange
        var alerts = new List<Alert>
        {
            new() { Symbol = "BTCUSDT", Interval = "5m" }
        };
        var strategy = new Strategy { Id = "123", Symbol = "BTCUSDT", Interval = "5m", StrategyType = StrategyType.CloseBuy };

        _alertRepositoryMock
            .Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);
        _strategyRepositoryMock
            .Setup(x => x.FindActiveStrategies())
            .ReturnsAsync(new Dictionary<string, Strategy> { { "123", strategy } });

        _klineStreamManagerMock
            .Setup(x => x.NeedsReconnection())
            .Returns(true);

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
        _klineStreamManagerMock.Verify(
            x => x.SubscribeSymbols(
                It.IsAny<HashSet<string>>(),
                It.IsAny<HashSet<string>>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionFails_ShouldLogError()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");

        _alertRepositoryMock
            .Setup(x => x.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        _klineStreamManagerMock
            .Setup(x => x.SubscribeSymbols(
                It.IsAny<HashSet<string>>(),
                It.IsAny<HashSet<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Initial subscription failed. Retrying in 1 minute...")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
