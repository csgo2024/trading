using System.ComponentModel.DataAnnotations;
using Moq;
using Trading.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class CreateStrategyCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _mockRepository;
    private readonly CreateStrategyCommandHandler _handler;

    public CreateStrategyCommandHandlerTests()
    {
        _mockRepository = new Mock<IStrategyRepository>();
        _handler = new CreateStrategyCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateAndReturnStrategy()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            PriceDropPercentage = 0.05m,
            AccountType = AccountType.Spot,
            Amount = 100,
            Leverage = 10
        };

        Strategy? capturedEntity = null;
        _mockRepository.Setup(x => x.Add(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .Callback<Strategy, CancellationToken>((strategy, token) => capturedEntity = strategy)
            .ReturnsAsync((Strategy strategy, CancellationToken token) => strategy);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedEntity);

        // Verify entity properties
        Assert.Equal(command.Symbol, result.Symbol);
        Assert.Equal(command.PriceDropPercentage, result.PriceDropPercentage);
        Assert.Equal(command.AccountType, result.AccountType);
        Assert.Equal(command.Amount, result.Amount);
        Assert.Equal(command.Leverage, result.Leverage);
        Assert.Equal(StateStatus.Running, result.Status);
        Assert.True(result.CreatedAt <= DateTime.Now);
        Assert.True(result.CreatedAt > DateTime.Now.AddMinutes(-1));

        // Verify repository method was called
        _mockRepository.Verify(x => x.Add(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidAmount_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 5, // Invalid: less than minimum 10
            PriceDropPercentage = 0.5m,
            Leverage = 10,
            AccountType = AccountType.Spot
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithInvalidPriceDropPercentage_ShouldThrowValidationException()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            PriceDropPercentage = 1.0m, // Invalid: greater than maximum 0.9
            Leverage = 10,
            AccountType = AccountType.Spot
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
