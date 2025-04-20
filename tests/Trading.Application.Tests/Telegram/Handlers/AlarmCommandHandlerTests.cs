using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Trading.Application.Commands;
using Trading.Application.Telegram.Handlers;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class AlarmCommandHandlerTests
{
    private readonly Mock<ILogger<AlarmCommandHandler>> _loggerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly AlarmCommandHandler _handler;

    public AlarmCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AlarmCommandHandler>>();
        _mediatorMock = new Mock<IMediator>();
        _alarmRepositoryMock = new Mock<IAlarmRepository>();
        _handler = new AlarmCommandHandler(
            _loggerMock.Object,
            _mediatorMock.Object,
            _alarmRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyParameters_LogsError()
    {
        // Act
        await _handler.HandleAsync("");

        // Assert
        VerifyLogError("Invalid command format");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyCommand_ClearsAllAlarms()
    {
        // Arrange
        _alarmRepositoryMock
            .Setup(x => x.ClearAllAlarmsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        await _handler.HandleAsync("empty");

        // Assert
        _alarmRepositoryMock.Verify(x => x.ClearAllAlarmsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(x => x.Publish(It.IsAny<AlarmEmptyedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyLogInfo("已清空所有价格警报");
    }

    [Fact]
    public async Task HandleAsync_WithCreateCommand_CreatesAlarm()
    {
        // Arrange
        var alarmJson = """{"Symbol":"BTCUSDT","Expression":"close > 1000","Interval":"1h"}""";
        var command = new CreateAlarmCommand { Symbol = "BTCUSDT", Expression = "close > 1000", Interval = "1h" };

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<CreateAlarmCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Alarm());

        // Act
        await _handler.HandleAsync($"create {alarmJson}");

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<CreateAlarmCommand>(c =>
                c.Symbol == command.Symbol &&
                c.Expression == command.Expression &&
                c.Interval == command.Interval),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCreateJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "invalid json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonReaderException>(() =>
            _handler.HandleAsync($"create {invalidJson}"));
    }

    [Fact]
    public async Task HandleAsync_WithDeleteCommand_DeletesAlarm()
    {
        // Arrange
        var alarmId = "test-alarm-id";
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<DeleteAlarmCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"delete {alarmId}");

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<DeleteAlarmCommand>(c => c.Id == alarmId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDeleteCommandFails_ThrowsException()
    {
        // Arrange
        var alarmId = "test-alarm-id";
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<DeleteAlarmCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync($"delete {alarmId}"));
        Assert.Contains(alarmId, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithPauseCommand_PausesAlarm()
    {
        // Arrange
        var alarmId = "test-alarm-id";
        var alarm = new Alarm { Id = alarmId, IsActive = true };

        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarm);

        // Act
        await _handler.HandleAsync($"pause {alarmId}");

        // Assert
        _alarmRepositoryMock.Verify(x => x.UpdateAsync(
            alarmId,
            It.Is<Alarm>(a => !a.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(x => x.Publish(
            It.Is<AlarmPausedEvent>(e => e.AlarmId == alarmId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithResumeCommand_ResumesAlarm()
    {
        // Arrange
        var alarmId = "test-alarm-id";
        var alarm = new Alarm { Id = alarmId, IsActive = false };

        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarm);

        // Act
        await _handler.HandleAsync($"resume {alarmId}");

        // Assert
        _alarmRepositoryMock.Verify(
            x => x.UpdateAsync(alarm.Id, alarm, It.IsAny<CancellationToken>()),
            Times.Once);
        _mediatorMock.Verify(x => x.Publish(
            It.Is<AlarmResumedEvent>(e => e.Alarm == alarm),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    public async Task HandleAsync_WithNonexistentAlarm_LogsError(string command)
    {
        // Arrange
        var alarmId = "nonexistent-id";
        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alarm?)null);

        // Act
        await _handler.HandleAsync($"{command} {alarmId}");

        // Assert
        VerifyLogError($"未找到报警 ID: {alarmId}");
    }

    [Theory]
    [InlineData("pause", true)]
    [InlineData("resume", false)]
    public async Task HandleCallbackAsync_WithValidCallback_PauseOrResumeAlarm(string action, bool isActive)
    {
        // Arrange
        var alarmId = "test-alarm-id";
        var alarm = new Alarm { Id = alarmId, IsActive = isActive };

        _alarmRepositoryMock
            .Setup(x => x.GetByIdAsync(alarmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alarm);

        // Act
        await _handler.HandleCallbackAsync(action, alarmId);

        // Assert
        _alarmRepositoryMock.Verify(x => x.UpdateAsync(
            alarmId,
            It.Is<Alarm>(a => a.IsActive == !isActive),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyLogError(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    private void VerifyLogInfo(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
}
