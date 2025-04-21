using MediatR;
using Moq;
using Trading.Application.Commands;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class DeleteAlertCommandHandlerTests
{
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly DeleteAlertCommandHandler _handler;

    public DeleteAlertCommandHandlerTests()
    {
        _alertRepositoryMock = new Mock<IAlertRepository>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new DeleteAlertCommandHandler(_alertRepositoryMock.Object, _mediatorMock.Object);
    }

    [Fact]
    public async Task Handle_WhenAlertExists_ShouldDeleteAndPublishEvent()
    {
        // Arrange
        var alertId = "test-alert-id";
        var command = new DeleteAlertCommand { Id = alertId };

        _alertRepositoryMock
            .Setup(x => x.DeleteAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify repository call
        _alertRepositoryMock.Verify(
            x => x.DeleteAsync(alertId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify event publication
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<AlertDeletedEvent>(e => e.AlertId == alertId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlertDoesNotExist_ShouldReturnFalseAndNotPublishEvent()
    {
        // Arrange
        var alertId = "non-existent-alert-id";
        var command = new DeleteAlertCommand { Id = alertId };

        _alertRepositoryMock
            .Setup(x => x.DeleteAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        // Verify repository call
        _alertRepositoryMock.Verify(
            x => x.DeleteAsync(alertId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no event was published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlertDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));

        // Verify no repository calls or events
        _alertRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlertDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var alertId = "test-alert-id";
        var command = new DeleteAlertCommand { Id = alertId };
        var expectedException = new InvalidOperationException("Database error");

        _alertRepositoryMock
            .Setup(x => x.DeleteAsync(alertId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Same(expectedException, exception);

        // Verify no event was published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlertDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
