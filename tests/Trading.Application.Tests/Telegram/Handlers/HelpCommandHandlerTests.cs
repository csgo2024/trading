using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Handlers;
using Trading.Common.Models;

namespace Trading.Application.Tests.Telegram.Handlers;

public class HelpCommandHandlerTests
{
    private readonly Mock<ILogger<HelpCommandHandler>> _loggerMock;
    private readonly Mock<ITelegramBotClient> _botClientMock;
    private readonly HelpCommandHandler _handler;
    private readonly string _testChatId = "456456481";

    public HelpCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<HelpCommandHandler>>();
        _botClientMock = new Mock<ITelegramBotClient>();
        var settings = new TelegramSettings { ChatId = _testChatId };
        var optionsMock = new Mock<IOptions<TelegramSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settings);

        _handler = new HelpCommandHandler(
            _loggerMock.Object,
            _botClientMock.Object,
            optionsMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSendRequest()
    {
        // arrange
        _botClientMock
            .Setup(x => x.SendRequest(It.IsAny<SendMessageRequest>(), default))
            .ReturnsAsync(new Message());
        // Act
        await _handler.HandleAsync("");

        // Assert
        _botClientMock.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.ChatId == _testChatId &&
                r.Text.Contains("基础命令") &&
                r.ParseMode == ParseMode.MarkdownV2),
            default),
            Times.Once);
    }
    [Fact]
    public async Task HandleCallbackAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _handler.HandleCallbackAsync("create", "123"));
    }
}
