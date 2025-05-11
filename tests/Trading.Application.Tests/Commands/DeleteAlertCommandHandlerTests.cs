using Moq;
using Trading.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class DeleteAlertCommandHandlerTests
{
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly DeleteAlertCommandHandler _handler;

    public DeleteAlertCommandHandlerTests()
    {
        _alertRepositoryMock = new Mock<IAlertRepository>();
        _handler = new DeleteAlertCommandHandler(_alertRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenAlertExists_ShouldDelete()
    {
        // Arrange
        var alert = new Alert { Id = "test-alert-id" };
        var command = new DeleteAlertCommand
        {
            Id = alert.Id
        };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alert.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        _alertRepositoryMock
            .Setup(x => x.DeleteAsync(alert, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify repository call
        _alertRepositoryMock.Verify(
            x => x.DeleteAsync(alert, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlertDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var alert = new Alert { Id = "non-existent-alert-id" };
        var command = new DeleteAlertCommand
        {
            Id = alert.Id
        };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alert.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as Alert);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        // Verify repository call
        _alertRepositoryMock.Verify(
            x => x.DeleteAsync(alert, It.IsAny<CancellationToken>()),
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
            x => x.DeleteAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        const string alertId = "test-alert-id";
        var alert = new Alert { Id = alertId };
        var command = new DeleteAlertCommand { Id = alertId };
        var expectedException = new InvalidOperationException("Database error");

        _alertRepositoryMock
            .Setup(x => x.DeleteAsync(alert, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alert.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Same(expectedException, exception);
    }
}
