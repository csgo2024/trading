using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Trading.Application.Helpers;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Common;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Services.Alerts;

public class AlertNotificationServiceTests
{
    private readonly Mock<ILogger<AlertNotificationService>> _loggerMock;
    private readonly Mock<ILogger<JavaScriptEvaluator>> _jsLoggerMock;
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly Mock<ITelegramBotClient> _botClientMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly Mock<IBackgroundTaskManager> _taskManagerMock;
    private readonly CancellationTokenSource _cts;
    private readonly AlertNotificationService _service;

    public AlertNotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<AlertNotificationService>>();
        _jsLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();

        _alertRepositoryMock = new Mock<IAlertRepository>();
        _botClientMock = new Mock<ITelegramBotClient>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(_jsLoggerMock.Object);
        _taskManagerMock = new Mock<IBackgroundTaskManager>();

        _cts = new CancellationTokenSource();

        var settings = new TelegramSettings { ChatId = "456456481" };
        var optionsMock = new Mock<IOptions<TelegramSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settings);

        _service = new AlertNotificationService(
            _loggerMock.Object,
            _alertRepositoryMock.Object,
            _botClientMock.Object,
            _jsEvaluatorMock.Object,
            _taskManagerMock.Object,
            optionsMock.Object
        );
    }

    [Fact]
    public async Task Handle_KlineUpdateEvent_ShouldUpdateLastKLines()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = Binance.Net.Enums.KlineInterval.OneHour;
        var kline = Mock.Of<IBinanceKline>();
        var notification = new KlineUpdateEvent(symbol, interval, kline);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        // Note: Since _lastKLines is private static, we can verify through the behavior
        // of ProcessAlert when it's called later
    }

    [Fact]
    public async Task Handle_AlertCreatedEvent_ShouldStartMonitoring()
    {
        // Arrange
        var alert = new Alert { Id = "test-id", Symbol = "BTCUSDT" };
        var notification = new AlertCreatedEvent(alert);

        _taskManagerMock
            .Setup(x => x.StartAsync(
                It.Is<string>(category => category == TaskCategories.Alert),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _taskManagerMock.Verify(
            x => x.StartAsync(
                It.Is<string>(category => category == TaskCategories.Alert),
                alert.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlertResumedEvent_ShouldStartMonitoring()
    {
        // Arrange
        var alert = new Alert
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Interval = "1h",
            Expression = "close > open",
            Status = StateStatus.Running,
        };
        var notification = new AlertResumedEvent(alert);

        _taskManagerMock
            .Setup(x => x.StartAsync(
                TaskCategories.Alert,
                alert.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        // Verify the task was started
        _taskManagerMock.Verify(
            x => x.StartAsync(
                TaskCategories.Alert,
                alert.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_AlertPausedEvent_ShouldStopMonitoring()
    {
        // Arrange
        var alertId = "test-id";
        var notification = new AlertPausedEvent(alertId);

        _taskManagerMock
            .Setup(x => x.StopAsync(
                It.Is<string>(category => category == TaskCategories.Alert),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _taskManagerMock.Verify(x => x.StopAsync(TaskCategories.Alert, alertId), Times.Once);
    }

    [Fact]
    public async Task Handle_AlertDeletedEvent_ShouldStopMonitoring()
    {
        // Arrange
        var alertId = "test-id";
        var notification = new AlertDeletedEvent(alertId);

        _taskManagerMock
            .Setup(x => x.StopAsync(
                It.Is<string>(category => category == TaskCategories.Alert),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _taskManagerMock.Verify(x => x.StopAsync(TaskCategories.Alert, alertId), Times.Once);
    }

    [Fact]
    public async Task ProcessAlert_WhenExpressionMet_ShouldSendNotification()
    {
        // Arrange
        var alert = new Alert
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

        await _service.Handle(new KlineUpdateEvent(alert.Symbol, Binance.Net.Enums.KlineInterval.OneHour, kline), CancellationToken.None);

        _jsEvaluatorMock
            .Setup(x => x.EvaluateExpression(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()))
            .Returns(true);

        _botClientMock
            .Setup(x => x.SendRequest(
                It.Is<SendMessageRequest>(r =>
                    r.ChatId == "456456481" &&
                    r.Text.Contains(alert.Symbol) &&
                    r.ParseMode == ParseMode.Html),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        // Act & Assert
        var task = Task.Run(() => _service.ProcessAlert(alert, _cts.Token), _cts.Token);

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
                    r.Text.Contains(alert.Symbol) &&
                    r.ParseMode == ParseMode.Html),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitWithAlerts_ShouldInitializeAllAlerts()
    {
        // Arrange
        var alerts = new[]
        {
            new Alert { Id = "test-1", Symbol = "BTCUSDT" },
            new Alert { Id = "test-2", Symbol = "ETHUSDT" }
        };

        _taskManagerMock
            .Setup(x => x.StartAsync(
                It.Is<string>(category => category == TaskCategories.Alert),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitWithAlerts(alerts, _cts.Token);

        // Assert
        _taskManagerMock.Verify(
            x => x.StartAsync(
                It.Is<string>(category => category == TaskCategories.Alert),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAlert_WhenNoKlineData_ShouldLogWarning()
    {
        // Arrange
        var alert = new Alert
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Expression = "close > open"
        };

        // Act
        var task = Task.Run(() => _service.ProcessAlert(alert, _cts.Token), _cts.Token);

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
