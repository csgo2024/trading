using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Telegram.Bot.Types.Enums;
using Trading.Application.Commands;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Domain.Entities;
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
    public async Task HandleAsync_WithEmptyParameters_LogInformation_WhenNoStrategies()
    {
        // arrange
        _strategyRepositoryMock.Setup(x => x.GetAllStrategies())
            .ReturnsAsync([]);
        // Act
        await _handler.HandleAsync("");

        // Assert
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, "Strategy is empty, please create and call later.");
    }

    [Theory]
    [InlineData(Status.Running, "Running")]
    [InlineData(Status.Paused, "Paused")]
    public async Task HandleAsync_WithEmptyParameters_ShouldReturnStrategyInformation(Status status, string statusText)
    {
        // arrange
        _strategyRepositoryMock.Setup(x => x.GetAllStrategies())
            .ReturnsAsync([new Strategy()
                {
                    Symbol = "BTCUSDT",
                    AccountType = AccountType.Spot,
                    Status = status,
                }
            ]);
        // Act
        await _handler.HandleAsync("");

        // Assert
        _loggerMock.Verify(
            x => x.BeginScope(
                It.Is<TelegramLoggerScope>(x => x.ParseMode == ParseMode.Html)),
            Times.Once);
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, statusText);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidSubCommand_ShouldLogError()
    {
        // Act
        await _handler.HandleAsync("invalid xyz");

        // Assert
        _loggerMock.VerifyLoggingOnce(LogLevel.Error, "Unknown command. Use: create, delete, pause, or resume");
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
                "Volatility": 0.01,
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
        await Assert.ThrowsAsync<JsonException>(
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
    public async Task HandlePause_WithValidId_ShouldUpdateStatus()
    {
        // Arrange
        const string strategyId = "test-id";
        _strategyRepositoryMock
            .Setup(x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Strategy()
            {
                Id = strategyId,
                Symbol = "BTCUSDT",
                AccountType = AccountType.Spot,
            });

        _strategyRepositoryMock
            .Setup(x => x.UpdateAsync(strategyId, It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"pause {strategyId}");

        // Assert
        _strategyRepositoryMock.Verify(
            x => x.UpdateAsync(
                strategyId,
                It.Is<Strategy>(x => x.Status == Status.Paused),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleResume_WithValidId_ShouldUpdateStatus()
    {
        // Arrange
        const string strategyId = "test-id";
        var strategy = new Strategy { Id = strategyId, Symbol = "BTCUSDT", AccountType = AccountType.Spot, };

        _strategyRepositoryMock
            .Setup(x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);
        _strategyRepositoryMock
            .Setup(x => x.UpdateAsync(strategyId, It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"resume {strategyId}");

        // Assert
        _strategyRepositoryMock.Verify(
            x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()),
            Times.Once);

        _strategyRepositoryMock.Verify(
            x => x.UpdateAsync(
                strategyId,
                It.Is<Strategy>(x => x.Status == Status.Running),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
