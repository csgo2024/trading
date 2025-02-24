using Moq;
using Trading.API.Application.Commands;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.Application.Commands;

public class DeleteStrategyCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _mockRepository;
    private readonly DeleteStrategyCommandHandler _handler;

    public DeleteStrategyCommandHandlerTests()
    {
        _mockRepository = new Mock<IStrategyRepository>();
        _handler = new DeleteStrategyCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ShouldCallDeleteAsync_AndReturnTrue()
    {
        // Arrange
        var command = new DeleteStrategyCommand { Id = "test-id" };
        _mockRepository.Setup(x => x.DeleteAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(x => x.DeleteAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeleteFails_ShouldReturnFalse()
    {
        // Arrange
        var command = new DeleteStrategyCommand { Id = "test-id" };
        _mockRepository.Setup(x => x.DeleteAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        _mockRepository.Verify(x => x.DeleteAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
