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

public class StrategyCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<StrategyCommandHandler>> _loggerMock;
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly StrategyCommandHandler _handler;

    public StrategyCommandHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<StrategyCommandHandler>>();
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        _handler = new StrategyCommandHandler(
            _mediatorMock.Object,
            _loggerMock.Object,
            _strategyRepositoryMock.Object);
    }

    [Fact]
    public void Command_ShouldReturnCorrectValue()
    {
        Assert.Equal("/strategy", StrategyCommandHandler.Command);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyParameters_ShouldLogError()
    {
        // Act
        await _handler.HandleAsync("");

        // Assert
        VerifyLogError("Invalid command format. Use: /strategy [create|delete|pause|resume] [parameters]");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidSubCommand_ShouldLogError()
    {
        // Act
        await _handler.HandleAsync("invalid xyz");

        // Assert
        VerifyLogError("Unknown command. Use: create, delete, pause, or resume");
    }

    [Fact]
    public async Task HandleCreate_WithValidJson_ShouldSendCommand()
    {
        // Arrange
        var json = """
            {
                "symbol": "BTCUSDT",
                "accountType": 0,
                "amount": 100,
                "priceDropPercentage": 0.01,
                "strategyType": 0
            }
            """;

        // Act
        await _handler.HandleAsync($"create {json}");

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<CreateStrategyCommand>(cmd =>
                    cmd.Symbol == "BTCUSDT"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCreate_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "invalid json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonReaderException>(
            () => _handler.HandleAsync($"create {invalidJson}"));
    }

    [Fact]
    public async Task HandleCreate_WithEmptyJson_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.HandleAsync("create "));
    }

    [Fact]
    public async Task HandleDelete_WithValidId_ShouldSendCommand()
    {
        // Arrange
        const string strategyId = "test-id";
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<DeleteStrategyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"delete {strategyId}");

        // Assert
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<DeleteStrategyCommand>(cmd => cmd.Id == strategyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDelete_WithEmptyId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.HandleAsync("delete "));
    }

    [Fact]
    public async Task HandleDelete_WhenDeleteFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const string strategyId = "test-id";
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<DeleteStrategyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync($"delete {strategyId}"));
        Assert.Equal($"Failed to delete strategy {strategyId}", exception.Message);
    }

    [Fact]
    public async Task HandlePause_WithValidId_ShouldUpdateStatusAndPublishEvent()
    {
        // Arrange
        const string strategyId = "test-id";
        _strategyRepositoryMock
            .Setup(x => x.UpdateStatusAsync(StateStatus.Paused))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"pause {strategyId}");

        // Assert
        _strategyRepositoryMock.Verify(
            x => x.UpdateStatusAsync(StateStatus.Paused),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<StrategyPausedEvent>(e => e.Id == strategyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleResume_WithValidId_ShouldUpdateStatusAndPublishEvent()
    {
        // Arrange
        const string strategyId = "test-id";
        var strategy = new Strategy { Id = strategyId };

        _strategyRepositoryMock
            .Setup(x => x.UpdateStatusAsync(StateStatus.Running))
            .ReturnsAsync(true);

        _strategyRepositoryMock
            .Setup(x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        // Act
        await _handler.HandleAsync($"resume {strategyId}");

        // Assert
        _strategyRepositoryMock.Verify(
            x => x.UpdateStatusAsync(StateStatus.Running),
            Times.Once);

        _strategyRepositoryMock.Verify(
            x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<StrategyResumedEvent>(e => e.Strategy == strategy),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _handler.HandleCallbackAsync("any-data"));
    }

    private void VerifyLogError(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(expectedMessage)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
