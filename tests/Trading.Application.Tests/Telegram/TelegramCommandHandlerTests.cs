using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Types;
using Trading.Application.Telegram;
using Trading.Application.Telegram.Handlers;

namespace Trading.Application.Tests.Telegram;

public class TelegramCommandHandlerTests
{
    private readonly Mock<ILogger<TelegramCommandHandler>> _mockLogger;
    private readonly Mock<TelegramCommandHandlerFactory> _mockHandlerFactory;
    private readonly Mock<ICommandHandler> _mockCommandHandler;
    private readonly TelegramCommandHandler _handler;

    public TelegramCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<TelegramCommandHandler>>();
        _mockHandlerFactory = new Mock<TelegramCommandHandlerFactory>(Mock.Of<IServiceProvider>());
        _mockCommandHandler = new Mock<ICommandHandler>();
        _handler = new TelegramCommandHandler(_mockLogger.Object, _mockHandlerFactory.Object);
    }

    [Fact]
    public async Task HandleCommand_WithNullText_ShouldReturnWithoutProcessing()
    {
        // Arrange
        var message = new Message { Text = null };

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCommand_WithEmptyText_ShouldReturnWithoutProcessing()
    {
        // Arrange  
        var message = new Message { Text = "" };

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("/help", "", "/help")]
    [InlineData("/alert", "", "/alert")]
    [InlineData("/strategy create btc", "create btc", "/strategy")]
    public async Task HandleCommand_WithValidCommand_ShouldProcessCorrectly(
        string input, string expectedParams, string expectedCommand)
    {
        // Arrange
        var message = new Message { Text = input };
        _mockHandlerFactory
            .Setup(x => x.GetHandler(expectedCommand))
            .Returns(_mockCommandHandler.Object);

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(expectedCommand),
            Times.Once);

        _mockCommandHandler.Verify(
            x => x.HandleAsync(expectedParams),
            Times.Once);
    }

    [Fact]
    public async Task HandleCommand_WhenHandlerNotFound_ShouldNotThrowException()
    {
        // Arrange
        var message = new Message { Text = "/unknowncommand" };
        _mockHandlerFactory
            .Setup(x => x.GetHandler(It.IsAny<string>()))
            .Returns((ICommandHandler?)null);

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockLogger.VerifyLoggingNever(LogLevel.Error, "");
    }

    [Fact]
    public async Task HandleCommand_WhenHandlerThrowsException_ShouldLogError()
    {
        // Arrange
        var message = new Message { Text = "/command" };
        var expectedException = new InvalidOperationException("Test exception");

        _mockHandlerFactory
            .Setup(x => x.GetHandler(It.IsAny<string>()))
            .Returns(_mockCommandHandler.Object);

        _mockCommandHandler
            .Setup(x => x.HandleAsync(It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "Command execution failed");
    }

    [Fact]
    public async Task HandleCallbackQuery_WithNullQuery_ShouldDoNothing()
    {
        // Act
        await _handler.HandleCallbackQuery(null);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallbackQuery_WithEmptyData_ShouldDoNothing()
    {
        // Arrange
        var query = new CallbackQuery { Data = "" };

        // Act  
        await _handler.HandleCallbackQuery(query);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("alert_pause_123", "alert", "pause", "123")]
    [InlineData("strategy_resume_456", "strategy", "resume", "456")]
    public async Task HandleCallbackQuery_WithValidData_ShouldProcessCorrectly(
        string data, string expectedPrefix, string expectedAction, string expectedParams)
    {
        // Arrange
        var query = new CallbackQuery { Data = data };
        _mockHandlerFactory
            .Setup(x => x.GetHandler(expectedPrefix))
            .Returns(_mockCommandHandler.Object);

        // Act
        await _handler.HandleCallbackQuery(query);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(expectedPrefix),
            Times.Once);

        _mockCommandHandler.Verify(
            x => x.HandleCallbackAsync(expectedAction, expectedParams),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackQuery_WithInvalidFormat_ShouldDoNothing()
    {
        // Arrange
        var query = new CallbackQuery { Data = "invalid_format" };

        // Act
        await _handler.HandleCallbackQuery(query);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallbackQuery_WhenHandlerThrowsException_ShouldLogError()
    {
        // Arrange
        var query = new CallbackQuery { Data = "alert_pause_123" };
        var expectedException = new InvalidOperationException("Test exception");

        _mockHandlerFactory
            .Setup(x => x.GetHandler(It.IsAny<string>()))
            .Returns(_mockCommandHandler.Object);

        _mockCommandHandler
            .Setup(x => x.HandleCallbackAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.HandleCallbackQuery(query);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "");
    }
}
