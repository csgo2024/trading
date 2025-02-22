using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Types;
using Trading.API.Application.Telegram;
using Trading.API.Application.Telegram.Handlers;

namespace Trading.API.Tests.Application.Telegram;

public class TelegramCommandHandlerTests
{
    private readonly Mock<ILogger<TelegramCommandHandler>> _mockLogger;
    private readonly Mock<TelegramCommandHandlerFactory> _mockHandlerFactory;
    private readonly Mock<ICommandHandler> _mockCommandHandler;
    private readonly TelegramCommandHandler _handler;

    public TelegramCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<TelegramCommandHandler>>();
        var services = new ServiceCollection();

        var serviceProvider = services.BuildServiceProvider();
        _mockHandlerFactory = new Mock<TelegramCommandHandlerFactory>(serviceProvider);
        _mockCommandHandler = new Mock<ICommandHandler>();
        _handler = new TelegramCommandHandler(_mockLogger.Object, _mockHandlerFactory.Object);
    }

    [Fact]
    public async Task HandleCommand_WithNullMessage_ShouldReturnWithoutProcessing()
    {
        // Act
        await _handler.HandleCommand(null);

        // Assert
        _mockHandlerFactory.Verify(
            x => x.GetHandler(It.IsAny<string>()),
            Times.Never,
            "Factory should not be called with null message");
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
            Times.Never,
            "Factory should not be called with null text");
    }

    [Theory]
    [InlineData("/start", "", "/start")]
    [InlineData("/help", "", "/help")]
    [InlineData("/create BTCUSDT", "BTCUSDT", "/create")]
    [InlineData("/delete 123", "123", "/delete")]
    [InlineData("/stop strategy1", "strategy1", "/stop")]
    [InlineData("/resume strategy2", "strategy2", "/resume")]
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
            Times.Once,
            "Factory should be called with correct command");
        
        _mockCommandHandler.Verify(
            x => x.HandleAsync(expectedParams),
            Times.Once,
            "Handler should be called with correct parameters");
    }

    [Fact]
    public async Task HandleCommand_WhenHandlerNotFound_ShouldNotThrowException()
    {
        // Arrange
        var message = new Message { Text = "/unknowncommand" };
        _mockHandlerFactory
            .Setup(x => x.GetHandler(It.IsAny<string>()))
            .Returns((ICommandHandler)null);

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCommand_WhenHandlerThrowsException_ShouldLogError()
    {
        // Arrange
        var message = new Message { Text = "/errorcommand" };
        var expectedException = new Exception("Test exception");
        
        _mockHandlerFactory
            .Setup(x => x.GetHandler(It.IsAny<string>()))
            .Returns(_mockCommandHandler.Object);

        _mockCommandHandler
            .Setup(x => x.HandleAsync(It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCommand_WithMultipleParameters_ShouldProcessCorrectly()
    {
        // Arrange
        var message = new Message { Text = "/create BTCUSDT 100 0.01" };
        _mockHandlerFactory
            .Setup(x => x.GetHandler("/create"))
            .Returns(_mockCommandHandler.Object);

        // Act
        await _handler.HandleCommand(message);

        // Assert
        _mockCommandHandler.Verify(
            x => x.HandleAsync("BTCUSDT 100 0.01"),
            Times.Once,
            "Handler should receive all parameters as a single string");
    }
}