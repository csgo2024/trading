using Moq;
using Trading.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class DeleteStrategyCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly DeleteStrategyCommandHandler _handler;

    public DeleteStrategyCommandHandlerTests()
    {
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        _handler = new DeleteStrategyCommandHandler(_strategyRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenStrategyExists_ShouldDeleteAndPublishEvent()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-strategy-id" };
        var strategyId = strategy.Id;
        var command = new DeleteStrategyCommand
        {
            Id = strategy.Id
        };

        _strategyRepositoryMock
            .Setup(x => x.DeleteAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _strategyRepositoryMock
            .Setup(x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify repository call
        _strategyRepositoryMock.Verify(
            x => x.DeleteAsync(strategy, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStrategyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-strategy-id" };
        var command = new DeleteStrategyCommand
        {
            Id = strategy.Id
        };

        _strategyRepositoryMock
            .Setup(x => x.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as Strategy);

        _strategyRepositoryMock
            .Setup(x => x.DeleteAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        // Verify repository call
        _strategyRepositoryMock.Verify(
            x => x.DeleteAsync(strategy, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var strategy = new Strategy { Id = "test-strategy-id" };
        var command = new DeleteStrategyCommand
        {
            Id = strategy.Id
        };
        var expectedException = new InvalidOperationException("Database error");

        _strategyRepositoryMock
            .Setup(x => x.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        _strategyRepositoryMock
            .Setup(x => x.DeleteAsync(strategy, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }
}
