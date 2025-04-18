using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Trading.Application.Commands;
using Trading.Application.Telegram.Handlers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class StrategyCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<StrategyCommandHandler>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly StrategyCommandHandler _handler;

    public StrategyCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<StrategyCommandHandler>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _handler = new StrategyCommandHandler(_mockMediator.Object, _mockLogger.Object, _mockStrategyRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCommand_ShouldLogError()
    {
        // Act
        await _handler.HandleAsync("");

        // Assert
        VerifyLoggerCalled("Invalid command format. Use: /strategy [create|delete|stop|resume] [parameters]", LogLevel.Error);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownSubCommand_ShouldLogError()
    {
        // Act
        await _handler.HandleAsync("unknown");

        // Assert
        VerifyLoggerCalled("Unknown command. Use: create, delete, stop, or resume", LogLevel.Error);
    }

    [Fact]
    public async Task HandleAsync_Create_WithValidParameters_ShouldCreateStrategy()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            PriceDropPercentage = 0.1m,
            Leverage = 10,
            AccountType = AccountType.Spot
        };
        var parameters = $"create {JsonConvert.SerializeObject(command)}";

        CreateStrategyCommand? capturedCommand = null;
        var expectedStrategy = new Strategy
        {
            Symbol = command.Symbol,
            Amount = command.Amount,
            PriceDropPercentage = command.PriceDropPercentage,
            Leverage = command.Leverage,
            AccountType = command.AccountType,
            Status = StateStatus.Running
        };

        _mockMediator
            .Setup(x => x.Send(It.IsAny<CreateStrategyCommand>(), CancellationToken.None))
            .Callback<IRequest<Strategy>, CancellationToken>((cmd, _) => capturedCommand = (CreateStrategyCommand)cmd)
            .ReturnsAsync(expectedStrategy);

        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(command.Symbol, capturedCommand.Symbol);
        Assert.Equal(command.Amount, capturedCommand.Amount);
        Assert.Equal(command.PriceDropPercentage, capturedCommand.PriceDropPercentage);
        Assert.Equal(command.Leverage, capturedCommand.Leverage);
        Assert.Equal(command.AccountType, capturedCommand.AccountType);

        _mockMediator.Verify(x => x.Send(It.IsAny<CreateStrategyCommand>(), default), Times.Once);
        VerifyLoggerCalled("策略创建成功 ✅", LogLevel.Information);
    }

    [Fact]
    public async Task HandleAsync_Delete_WithValidId_ShouldDeleteStrategy()
    {
        // Arrange
        var strategyId = "test-id";
        var parameters = $"delete {strategyId}";

        _mockMediator
            .Setup(x => x.Send(It.IsAny<DeleteStrategyCommand>(), default))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        _mockMediator.Verify(x => x.Send(It.Is<DeleteStrategyCommand>(cmd => cmd.Id == strategyId), default), Times.Once);
        VerifyLoggerCalled($"策略[{strategyId}]已删除 ✅", LogLevel.Information);
    }

    [Fact]
    public async Task HandleAsync_Delete_WithFailure_ShouldLogError()
    {
        // Arrange
        var strategyId = "test-id";
        var parameters = $"delete {strategyId}";

        _mockMediator
            .Setup(x => x.Send(It.IsAny<DeleteStrategyCommand>(), default))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        VerifyLoggerError("Strategy command execution failed");
    }

    [Fact]
    public async Task HandleAsync_Stop_ShouldPauseAllStrategies()
    {
        // Arrange
        _mockStrategyRepository
            .Setup(x => x.UpdateStatusAsync(StateStatus.Paused))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync("stop");

        // Assert
        _mockStrategyRepository.Verify(x => x.UpdateStatusAsync(StateStatus.Paused), Times.Once);
        VerifyLoggerCalled("策略已成功暂停 ⏸️", LogLevel.Information);
    }

    [Fact]
    public async Task HandleAsync_Resume_ShouldResumeAllStrategies()
    {
        // Arrange
        _mockStrategyRepository
            .Setup(x => x.UpdateStatusAsync(StateStatus.Running))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync("resume");

        // Assert
        _mockStrategyRepository.Verify(x => x.UpdateStatusAsync(StateStatus.Running), Times.Once);
        VerifyLoggerCalled("策略已成功恢复运行️", LogLevel.Information);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("create ")]
    public async Task HandleAsync_Create_WithInvalidJson_ShouldLogError(string parameters)
    {
        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        _mockMediator.Verify(x => x.Send(It.IsAny<CreateStrategyCommand>(), default), Times.Never);
        VerifyLoggerError("Strategy command execution failed");
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("delete ")]
    public async Task HandleAsync_Delete_WithInvalidId_ShouldLogError(string parameters)
    {
        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        _mockMediator.Verify(x => x.Send(It.IsAny<DeleteStrategyCommand>(), default), Times.Never);
        VerifyLoggerError("Strategy command execution failed");
    }

    private void VerifyLoggerCalled(string message, LogLevel level)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLoggerError(string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
