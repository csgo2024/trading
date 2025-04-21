using MediatR;
using Moq;
using Trading.Application.Commands;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class DeleteStrategyCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly DeleteStrategyCommandHandler _handler;

    public DeleteStrategyCommandHandlerTests()
    {
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new DeleteStrategyCommandHandler(_strategyRepositoryMock.Object, _mediatorMock.Object);
    }

    [Fact]
    public async Task Handle_WhenStrategyExists_ShouldDeleteAndPublishEvent()
    {
        // Arrange
        var strategyId = "test-strategy-id";
        var command = new DeleteStrategyCommand
        {
            Id = strategyId
        };

        _strategyRepositoryMock
            .Setup(x => x.DeleteAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify repository call
        _strategyRepositoryMock.Verify(
            x => x.DeleteAsync(strategyId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify event publication
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<StrategyDeletedEvent>(e => e.Id == strategyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStrategyDoesNotExist_ShouldReturnFalseAndNotPublishEvent()
    {
        // Arrange
        var strategyId = "test-strategy-id";
        var command = new DeleteStrategyCommand
        {
            Id = strategyId
        };

        _strategyRepositoryMock
            .Setup(x => x.DeleteAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        // Verify repository call
        _strategyRepositoryMock.Verify(
            x => x.DeleteAsync(strategyId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no event was published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<StrategyDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var strategyId = "test-strategy-id";
        var command = new DeleteStrategyCommand
        {
            Id = strategyId
        };
        var expectedException = new InvalidOperationException("Database error");

        _strategyRepositoryMock
            .Setup(x => x.DeleteAsync(strategyId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Same(expectedException, exception);

        // Verify no event was published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<StrategyDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }
}
