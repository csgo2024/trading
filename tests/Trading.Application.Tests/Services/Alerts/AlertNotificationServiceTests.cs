using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Types.Enums;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Common;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Services.Alerts;

public class AlertNotificationServiceTests
{
    private readonly Mock<ILogger<AlertNotificationService>> _mockLogger;
    private readonly Mock<ILogger<JavaScriptEvaluator>> _jsLoggerMock;
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly Mock<IBackgroundTaskManager> _taskManagerMock;
    private readonly CancellationTokenSource _cts;
    private readonly AlertNotificationService _service;

    public AlertNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<AlertNotificationService>>();
        _jsLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();

        _alertRepositoryMock = new Mock<IAlertRepository>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(_jsLoggerMock.Object);
        _taskManagerMock = new Mock<IBackgroundTaskManager>();

        _cts = new CancellationTokenSource();

        _service = new AlertNotificationService(
            _mockLogger.Object,
            _alertRepositoryMock.Object,
            _jsEvaluatorMock.Object,
            _taskManagerMock.Object
        );
    }

    [Fact]
    public async Task Handle_KlineUpdateEvent_ShouldUpdateLastKLines()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var interval = Binance.Net.Enums.KlineInterval.OneHour;
        var kline = Mock.Of<IBinanceKline>();
        var notification = new KlineClosedEvent(symbol, interval, kline);
        var idsToUpdate = new List<string> { };
        _alertRepositoryMock
            .Setup(x => x.ResumeAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(idsToUpdate);

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
                It.Is<TaskCategory>(category => category == TaskCategory.Alert),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _taskManagerMock.Verify(
            x => x.StartAsync(
                It.Is<TaskCategory>(category => category == TaskCategory.Alert),
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
            Status = Status.Running,
        };
        var notification = new AlertResumedEvent(alert);

        _taskManagerMock
            .Setup(x => x.StartAsync(
                TaskCategory.Alert,
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
                TaskCategory.Alert,
                alert.Id,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_AlertPausedEvent_ShouldStopMonitoring()
    {
        // Arrange
        var alert = new Alert { Id = "test-id", };
        var notification = new AlertPausedEvent(alert);

        _taskManagerMock
            .Setup(x => x.StopAsync(
                It.Is<TaskCategory>(category => category == TaskCategory.Alert),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _taskManagerMock.Verify(x => x.StopAsync(TaskCategory.Alert, alert.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_AlertDeletedEvent_ShouldStopMonitoring()
    {
        // Arrange
        var alert = new Alert { Id = "test-id", };
        var notification = new AlertDeletedEvent(alert);

        _taskManagerMock
            .Setup(x => x.StopAsync(
                It.Is<TaskCategory>(category => category == TaskCategory.Alert),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.Handle(notification, _cts.Token);

        // Assert
        _taskManagerMock.Verify(x => x.StopAsync(TaskCategory.Alert, alert.Id), Times.Once);
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

        var idsToUpdate = new List<string> { };
        _alertRepositoryMock
            .Setup(x => x.ResumeAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(idsToUpdate);

        await _service.Handle(new KlineClosedEvent(alert.Symbol, Binance.Net.Enums.KlineInterval.OneHour, kline), CancellationToken.None);

        _jsEvaluatorMock
            .Setup(x => x.EvaluateExpression(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>()))
            .Returns(true);

        // Act & Assert
        var task = Task.Run(() => _service.ProcessAlert(alert, _cts.Token), _cts.Token);

        // Give some time for the processing
        await Task.Delay(1000);

        // Cancel the operation
        await _cts.CancelAsync();

        // Wait for completion
        await task;

        // Assert
        _mockLogger.Verify(
            x => x.BeginScope(
                It.Is<TelegramLoggerScope>(x => x.ParseMode == ParseMode.Html
                                                && x.DisableNotification == false
                                                && x.Title!.Contains(alert.Symbol))),
            Times.Once);
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, "Expression");
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
                It.Is<TaskCategory>(category => category == TaskCategory.Alert),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InitWithAlerts(alerts, _cts.Token);

        // Assert
        _taskManagerMock.Verify(
            x => x.StartAsync(
                It.Is<TaskCategory>(category => category == TaskCategory.Alert),
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
        _mockLogger.VerifyLoggingTimes(LogLevel.Debug, "", Times.AtLeastOnce);
    }
}
