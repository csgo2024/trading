using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.Logging;

namespace Trading.Application.Tests.Telegram.Handlers;

public class HelpCommandHandlerTests
{
    private readonly Mock<ILogger<HelpCommandHandler>> _loggerMock;
    private readonly HelpCommandHandler _handler;
    public HelpCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<HelpCommandHandler>>();

        _handler = new HelpCommandHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSendRequest()
    {
        // arrange
        // Act
        await _handler.HandleAsync("");

        // Assert
        _loggerMock.Verify(
            x => x.BeginScope(
                It.Is<TelegramLoggerScope>(x => x.ParseMode == ParseMode.MarkdownV2)),
            Times.Once);
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, "CloseBuy");

    }
    [Fact]
    public async Task HandleCallbackAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _handler.HandleCallbackAsync("create", "123"));
    }
}
