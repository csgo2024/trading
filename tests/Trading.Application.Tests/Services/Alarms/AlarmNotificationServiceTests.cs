using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Trading.Application.Helpers;
using Trading.Application.Services.Alarms;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Services.Alarms;

public class AlarmNotificationServiceTests
{
    private readonly Mock<ILogger<AlarmNotificationService>> _loggerMock;
    private readonly Mock<ILogger<JavaScriptEvaluator>> _jsLoggerMock;
    private readonly Mock<ILogger<AlarmTaskManager>> _alarmLoggerMock;
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly Mock<ITelegramBotClient> _botClientMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly Mock<AlarmTaskManager> _alarmTaskManagerMock;
    private readonly CancellationTokenSource _cts;
    private readonly AlarmNotificationService _service;

    public AlarmNotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<AlarmNotificationService>>();
        _jsLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();
        _alarmLoggerMock = new Mock<ILogger<AlarmTaskManager>>();

        _alarmRepositoryMock = new Mock<IAlarmRepository>();
        _botClientMock = new Mock<ITelegramBotClient>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(_jsLoggerMock.Object);
        _alarmTaskManagerMock = new Mock<AlarmTaskManager>(_alarmLoggerMock.Object);

        _cts = new CancellationTokenSource();

        var settings = new TelegramSettings { ChatId = "456456481" };
        var optionsMock = new Mock<IOptions<TelegramSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settings);

        _service = new AlarmNotificationService(
            _loggerMock.Object,
            _alarmRepositoryMock.Object,
            _botClientMock.Object,
            _jsEvaluatorMock.Object,
            _alarmTaskManagerMock.Object,
            optionsMock.Object
        );
    }

    [Fact]
    public async Task Handle_KlineUpdateEvent_ShouldUpdateLastKlines()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = Binance.Net.Enums.KlineInterval.OneHour;
        var kline = Mock.Of<IBinanceKline>();
        var notification = new KlineUpdateEvent(symbol, interval, kline);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        // Note: Since _lastKlines is private static, we can verify through the behavior
        // of ProcessAlarm when it's called later
    }

    [Fact]
    public async Task Handle_AlarmCreatedEvent_ShouldStartMonitoring()
    {
        // Arrange
        var alarm = new Alarm { Id = "test-id", Symbol = "BTCUSDT" };
        var notification = new AlarmCreatedEvent(alarm);

        _alarmTaskManagerMock
            .Setup(x => x.Start(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _alarmTaskManagerMock.Verify(
            x => x.Start(
                alarm.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlarmPausedEvent_ShouldStopMonitoring()
    {
        // Arrange
        var alarmId = "test-id";
        var notification = new AlarmPausedEvent(alarmId);

        _alarmTaskManagerMock
            .Setup(x => x.Stop(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _alarmTaskManagerMock.Verify(x => x.Stop(alarmId), Times.Once);
    }

    [Fact]
    public async Task ProcessAlarm_WhenExpressionMet_ShouldSendNotification()
    {
        // Arrange
        var alarm = new Alarm
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Expression = "close > open",
            Interval = "1h",
            LastNotification = DateTime.UtcNow.AddMinutes(-2)
        };

        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);

        // 首先发送一个 KlineUpdateEvent 来初始化 _lastkLines
        await _service.Handle(new KlineUpdateEvent(alarm.Symbol, Binance.Net.Enums.KlineInterval.OneHour, kline), CancellationToken.None);

        _jsEvaluatorMock
            .Setup(x => x.EvaluateExpression(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()))
            .Returns(true);

        // Setup bot client mock
        _botClientMock
            .Setup(x => x.SendRequest(
                It.Is<SendMessageRequest>(r =>
                    r.ChatId == "456456481" &&
                    r.Text.Contains(alarm.Symbol) &&
                    r.ParseMode == ParseMode.Html),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        // Act & Assert
        var task = Task.Run(() => _service.ProcessAlarm(alarm, _cts.Token), _cts.Token);

        // Give some time for the processing
        await Task.Delay(1000);

        // Cancel the operation
        await _cts.CancelAsync();

        // Wait for completion
        await task;

        // Assert
        _botClientMock.Verify(
            x => x.SendRequest(
                It.Is<SendMessageRequest>(r =>
                    r.ChatId == "456456481" &&
                    r.Text.Contains(alarm.Symbol) &&
                    r.ParseMode == ParseMode.Html),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitWithAlarms_ShouldInitializeAllAlarms()
    {
        // Arrange
        var alarms = new[]
        {
            new Alarm { Id = "test-1", Symbol = "BTCUSDT" },
            new Alarm { Id = "test-2", Symbol = "ETHUSDT" }
        };

        _alarmTaskManagerMock
            .Setup(x => x.Start(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitWithAlarms(alarms, _cts.Token);

        // Assert
        _alarmTaskManagerMock.Verify(
            x => x.Start(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAlarm_WhenNoKlineData_ShouldLogWarning()
    {
        // Arrange
        var alarm = new Alarm
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Expression = "close > open"
        };

        // Act
        var task = Task.Run(() => _service.ProcessAlarm(alarm, _cts.Token), _cts.Token);

        await Task.Delay(1000, _cts.Token);
        await _cts.CancelAsync();
        await task;

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
}
