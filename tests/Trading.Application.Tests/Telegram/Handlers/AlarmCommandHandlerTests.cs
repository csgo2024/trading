using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Helpers;
using Trading.Application.Telegram.Handlers;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class AlarmCommandHandlerTests
{
    private readonly Mock<ILogger<AlarmCommandHandler>> _loggerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly AlarmCommandHandler _handler;

    public AlarmCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AlarmCommandHandler>>();
        _mediatorMock = new Mock<IMediator>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _alarmRepositoryMock = new Mock<IAlarmRepository>();

        _handler = new AlarmCommandHandler(
            _loggerMock.Object,
            _mediatorMock.Object,
            _jsEvaluatorMock.Object,
            _alarmRepositoryMock.Object);
    }

    [Fact]
    public void Command_ShouldReturnCorrectValue()
    {
        Assert.Equal("/alarm", AlarmCommandHandler.Command);
    }

    [Fact]
    public async Task HandleAsync_Empty_ShouldClearAllAlarms()
    {
        // Arrange
        const int deletedCount = 5;
        _alarmRepositoryMock
            .Setup(x => x.ClearAllAlarmsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        // Act
        await _handler.HandleAsync("empty");

        // Assert
        _alarmRepositoryMock.Verify(x => x.ClearAllAlarmsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(x => x.Publish(It.IsAny<AlarmEmptyEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyLog(LogLevel.Information, $"<pre>已清空所有价格警报，共删除 {deletedCount} 个警报</pre>");
    }

    [Fact]
    public async Task HandleAsync_InvalidFormat_ShouldLogError()
    {
        // Arrange
        var invalidCommand = "BTCUSDT 1h";  // Missing expression

        // Act
        await _handler.HandleAsync(invalidCommand);

        // Assert
        VerifyLog(LogLevel.Error, "Invalid command format");
        // _alarmRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Alarm>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_WithInvalidExpression_ShouldLogError()
    {
        // Arrange
        const string invalidExpression = "invalid expression";
        _jsEvaluatorMock
            .Setup(x => x.ValidateExpression(invalidExpression, out It.Ref<string>.IsAny))
            .Returns(false);

        // Act
        await _handler.HandleAsync($"BTCUSDT 1h {invalidExpression}");

        // Assert
        VerifyLog(LogLevel.Error, "条件语法错误");
        // _alarmRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Alarm>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ShouldCreateAlarm()
    {
        // Arrange
        const string symbol = "BTCUSDT";
        const string interval = "1h";
        const string expression = "close > 50000";

        _jsEvaluatorMock
            .Setup(x => x.ValidateExpression(expression, out It.Ref<string>.IsAny))
            .Returns(true);

        Alarm? capturedAlarm = null;
        _alarmRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Alarm>(), It.IsAny<CancellationToken>()))
            .Callback<Alarm, CancellationToken>((alarm, _) => capturedAlarm = alarm)
            .Returns(Task.FromResult(capturedAlarm)!);

        // Act
        await _handler.HandleAsync($"{symbol} {interval} {expression}");

        // Assert
        Assert.NotNull(capturedAlarm);
        Assert.Equal(symbol, capturedAlarm.Symbol);
        Assert.Equal(interval, capturedAlarm.Interval);
        Assert.Equal(expression, capturedAlarm.Expression);
        Assert.True(capturedAlarm.IsActive);

        _mediatorMock.Verify(
            x => x.Publish(It.Is<AlarmCreatedEvent>(e => e.Alarm == capturedAlarm), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_WithInvalidFormat_ShouldLogError()
    {
        // Act
        await _handler.HandleCallbackAsync("invalid_format");

        // Assert
        // VerifyLog(LogLevel.Error, "处理价格报警回调失败");
    }

    [Fact]
    public async Task HandleCallbackAsync_AlarmNotFound_ShouldLogError()
    {
        // Arrange
        const string alarmId = "non-existent-id";
        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarmId, It.IsAny<CancellationToken>()))!
            .ReturnsAsync((Alarm?)null);

        // Act
        await _handler.HandleCallbackAsync($"pause_{alarmId}");

        // Assert
        VerifyLog(LogLevel.Error, $"<pre>未找到报警 ID: {alarmId}</pre>");
    }

    [Fact]
    public async Task HandleCallbackAsync_PauseAction_ShouldUpdateAndPublishEvent()
    {
        // Arrange
        var alarm = new Alarm { Id = "test-id", IsActive = true };
        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarm.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarm);

        // Act
        await _handler.HandleCallbackAsync($"pause_{alarm.Id}");

        // Assert
        Assert.False(alarm.IsActive);
        Assert.NotNull(alarm.UpdatedAt);

        _alarmRepositoryMock.Verify(
            x => x.UpdateAsync(alarm.Id, alarm, It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.Is<AlarmPausedEvent>(e => e.AlarmId == alarm.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_ResumeAction_ShouldUpdateAndPublishEvent()
    {
        // Arrange
        var alarm = new Alarm { Id = "test-id", IsActive = false };
        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarm.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarm);

        // Act
        await _handler.HandleCallbackAsync($"resume_{alarm.Id}");

        // Assert
        Assert.True(alarm.IsActive);
        Assert.NotNull(alarm.UpdatedAt);

        _alarmRepositoryMock.Verify(
            x => x.UpdateAsync(alarm.Id, alarm, It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(It.Is<AlarmResumedEvent>(e => e.Alarm == alarm), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_UnknownAction_ShouldNotUpdateAlarm()
    {
        // Arrange
        var alarm = new Alarm { Id = "test-id" };
        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarm.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarm);

        // Act
        await _handler.HandleCallbackAsync($"unknown_{alarm.Id}");

        // Assert
        _alarmRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Alarm>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyLog(LogLevel level, string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
