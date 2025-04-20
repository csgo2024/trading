using MediatR;
using Moq;
using Trading.Application.Commands;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class DeleteAlarmCommandHandlerTests
{
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly DeleteAlarmCommandHandler _handler;

    public DeleteAlarmCommandHandlerTests()
    {
        _alarmRepositoryMock = new Mock<IAlarmRepository>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new DeleteAlarmCommandHandler(_alarmRepositoryMock.Object, _mediatorMock.Object);
    }

    [Fact]
    public async Task Handle_WhenAlarmExists_ShouldDeleteAndPublishEvent()
    {
        // Arrange
        var alarmId = "test-alarm-id";
        var command = new DeleteAlarmCommand { Id = alarmId };

        _alarmRepositoryMock
            .Setup(x => x.DeleteAsync(alarmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify repository call
        _alarmRepositoryMock.Verify(
            x => x.DeleteAsync(alarmId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify event publication
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<AlarmDeletedEvent>(e => e.AlarmId == alarmId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlarmDoesNotExist_ShouldReturnFalseAndNotPublishEvent()
    {
        // Arrange
        var alarmId = "non-existent-alarm-id";
        var command = new DeleteAlarmCommand { Id = alarmId };

        _alarmRepositoryMock
            .Setup(x => x.DeleteAsync(alarmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        // Verify repository call
        _alarmRepositoryMock.Verify(
            x => x.DeleteAsync(alarmId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no event was published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlarmDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));

        // Verify no repository calls or events
        _alarmRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlarmDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var alarmId = "test-alarm-id";
        var command = new DeleteAlarmCommand { Id = alarmId };
        var expectedException = new Exception("Database error");

        _alarmRepositoryMock
            .Setup(x => x.DeleteAsync(alarmId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Same(expectedException, exception);

        // Verify no event was published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlarmDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
