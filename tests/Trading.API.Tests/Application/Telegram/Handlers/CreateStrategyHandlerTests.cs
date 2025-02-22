using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Trading.API.Application.Commands;
using Trading.API.Application.Telegram.Handlers;
using Trading.Domain.Entities;

namespace Trading.API.Tests.Application.Telegram.Handlers;

public class CreateStrategyHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreateStrategyHandler>> _mockLogger;
    private readonly CreateStrategyHandler _handler;

    public CreateStrategyHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreateStrategyHandler>>();
        _handler = new CreateStrategyHandler(_mockMediator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidParameters_ShouldCreateStrategy()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            PriceDropPercentage = 0.1m,
            Leverage = 10,
            StrategyType = StrategyType.Spot
        };
        var parameters = JsonConvert.SerializeObject(command);

        CreateStrategyCommand? capturedCommand = null;
        var expectedStrategy = new Strategy
        {
            Symbol = command.Symbol,
            Amount = command.Amount,
            PriceDropPercentage = command.PriceDropPercentage,
            Leverage = command.Leverage,
            StrategyType = command.StrategyType,
            Status = StrateStatus.Running
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
        Assert.Equal(command.StrategyType, capturedCommand.StrategyType);

        _mockMediator.Verify(x => x.Send(It.IsAny<CreateStrategyCommand>(), default), Times.Once);
        VerifyLoggerCalled("策略创建成功 ✅", LogLevel.Information);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task HandleAsync_WithInvalidParameters_ShouldLogError(string parameters)
    {
        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        _mockMediator.Verify(x => x.Send(It.IsAny<CreateStrategyCommand>(), default), Times.Never);
        VerifyLoggerError("Create strategy failed");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidJson_ShouldLogError()
    {
        // Arrange
        var invalidJson = "{invalid_json}";

        // Act
        await _handler.HandleAsync(invalidJson);

        // Assert
        _mockMediator.Verify(x => x.Send(It.IsAny<CreateStrategyCommand>(), default), Times.Never);
        VerifyLoggerError("Create strategy failed");
    }

    [Fact]
    public async Task HandleAsync_WhenMediatorThrows_ShouldLogError()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            PriceDropPercentage = 0.1m,
            Leverage = 10,
            StrategyType = StrategyType.Spot
        };
        var parameters = JsonConvert.SerializeObject(command);

        _mockMediator.Setup(x => x.Send(It.IsAny<CreateStrategyCommand>(), default))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.HandleAsync(parameters);

        // Assert
        VerifyLoggerError("Create strategy failed");
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