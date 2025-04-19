using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Trading.API.HostServices;
using Trading.Application.Helpers;
using Trading.Application.Services.Alarms;
using Trading.Application.Services.Common;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.HostServices;

public class AlarmHostServiceTests : IDisposable
{
    private readonly Mock<ILogger<AlarmHostService>> _loggerMock;
    private readonly Mock<IKlineStreamManager> _klineStreamManagerMock;
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly Mock<ITelegramBotClient> _botClientMock;
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManagerMock;
    private readonly CancellationTokenSource _cts;
    private readonly AlarmNotificationService _alarmNotificationService;
    private readonly TestAlarmHostService _service;

    public AlarmHostServiceTests()
    {
        _loggerMock = new Mock<ILogger<AlarmHostService>>();
        _klineStreamManagerMock = new Mock<IKlineStreamManager>();
        _alarmRepositoryMock = new Mock<IAlarmRepository>();
        _botClientMock = new Mock<ITelegramBotClient>();
        _backgroundTaskManagerMock = new Mock<IBackgroundTaskManager>();
        _cts = new CancellationTokenSource();

        // 创建AlarmNotificationService的依赖
        var notificationLoggerMock = new Mock<ILogger<AlarmNotificationService>>();
        var jsEvaluatorLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();
        var telegramSettings = Options.Create(new TelegramSettings { ChatId = "test-chat-id" });
        var jsEvaluator = new JavaScriptEvaluator(jsEvaluatorLoggerMock.Object);

        _alarmNotificationService = new AlarmNotificationService(
            notificationLoggerMock.Object,
            _alarmRepositoryMock.Object,
            _botClientMock.Object,
            jsEvaluator,
            _backgroundTaskManagerMock.Object,
            telegramSettings);

        _service = new TestAlarmHostService(
            _loggerMock.Object,
            _klineStreamManagerMock.Object,
            _alarmNotificationService,
            _alarmRepositoryMock.Object);

        SetupDefaults();
    }

    private sealed class TestAlarmHostService : AlarmHostService
    {
        private int _delayCallCount;

        public TestAlarmHostService(
            ILogger<AlarmHostService> logger,
            IKlineStreamManager klineStreamManager,
            AlarmNotificationService alarmNotificationService,
            IAlarmRepository alarmRepository)
            : base(logger, klineStreamManager, alarmNotificationService, alarmRepository)
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

        _alarmRepositoryMock
            .Setup(x => x.GetActiveAlarmsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alarm>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActiveAlarms_ShouldNotSubscribe()
    {
        // Arrange
        _alarmRepositoryMock
            .Setup(x => x.GetActiveAlarmsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alarm>());

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
    public async Task ExecuteAsync_WithActiveAlarms_ShouldSubscribe()
    {
        // Arrange
        var alarms = new List<Alarm>
        {
            new() { Symbol = "BTCUSDT", Interval = "5m" },
            new() { Symbol = "ETHUSDT", Interval = "15m" }
        };

        _alarmRepositoryMock
            .Setup(x => x.GetActiveAlarmsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarms);

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
                It.Is<HashSet<string>>(s => s.SetEquals(new[] { "BTCUSDT", "ETHUSDT" })),
                It.Is<HashSet<string>>(i => i.SetEquals(new[] { "5m", "15m" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNeedsReconnection_ShouldResubscribe()
    {
        // Arrange
        var alarms = new List<Alarm>
        {
            new() { Symbol = "BTCUSDT", Interval = "5m" }
        };

        _alarmRepositoryMock
            .Setup(x => x.GetActiveAlarmsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarms);

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

        _alarmRepositoryMock
            .Setup(x => x.GetActiveAlarmsAsync(It.IsAny<CancellationToken>()))
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
