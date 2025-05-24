using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Logging;
using Trading.Common.Models;

namespace Trading.Application.Tests.Telegram.Logging;

public class TelegramLoggerTests
{
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly TelegramLogger _logger;
    private readonly string _testChatId = "456456481";

    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;

    public TelegramLoggerTests()
    {
        _mockBotClient = new Mock<ITelegramBotClient>();
        var settings = new TelegramSettings { ChatId = _testChatId };
        _loggerOptions = Options.Create(new TelegramLoggerOptions
        {
            MinimumLevel = LogLevel.Trace,
            IncludeCategory = true,
            ExcludeCategories = new List<string>()
        });
        _logger = new TelegramLogger(_mockBotClient.Object, _loggerOptions, settings, "TestCategory");
    }

    [Fact]
    public async Task Log_WithInformationLevel_ShouldSendMessageWithCorrectEmoji()
    {
        // Arrange
        var logMessage = "Test log message";
        _mockBotClient
            .Setup(x => x.SendRequest(It.IsAny<SendMessageRequest>(), CancellationToken.None))
            .ReturnsAsync(new Message());

        // Act
        _logger.Log(LogLevel.Information, 0, logMessage, null, (state, _) => state.ToString());

        // Wait for async operation
        await Task.Delay(100);

        // Assert
        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.ChatId == _testChatId &&
                r.Text.Contains("ℹ️") &&
                r.Text.Contains(logMessage) &&
                r.ParseMode == ParseMode.Html),
            CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Log_WithException_ShouldIncludeExceptionDetails()
    {
        // Arrange
        var logMessage = "Test error message";
        Exception? exception = null;
        try
        {
            ThrowTestException(logMessage);
        }
        catch (Exception? ex)
        {
            exception = ex;
        }

        _mockBotClient
            .Setup(x => x.SendRequest(It.IsAny<SendMessageRequest>(), CancellationToken.None))
            .ReturnsAsync(new Message());

        // Act
        _logger.Log(LogLevel.Error, 0, logMessage, exception, (state, _) => state.ToString());

        // Wait for async operation
        await Task.Delay(100);

        // Assert
        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.ChatId == _testChatId &&
                r.Text.Contains('❌') &&
                r.Text.Contains(logMessage) &&
                exception != null &&
                r.Text.Contains(exception.Message) &&
                r.Text.Contains(exception.StackTrace!) &&
                r.ParseMode == ParseMode.Html),
            CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "🔍")]
    [InlineData(LogLevel.Debug, "🔧")]
    [InlineData(LogLevel.Information, "ℹ️")]
    [InlineData(LogLevel.Warning, "⚠️")]
    [InlineData(LogLevel.Error, "❌")]
    [InlineData(LogLevel.Critical, "🆘")]
    public async Task Log_WithDifferentLogLevels_ShouldUseCorrectEmoji(LogLevel level, string expectedEmoji)
    {
        // Arrange
        var logMessage = "Test message";
        _mockBotClient
            .Setup(x => x.SendRequest(It.IsAny<SendMessageRequest>(), CancellationToken.None))
            .ReturnsAsync(new Message());

        // Act
        _logger.Log(level, 0, logMessage, null, (state, _) => state.ToString());

        // Wait for async operation
        await Task.Delay(100);

        // Assert
        _mockBotClient.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.ChatId == _testChatId &&
                r.Text.Contains(expectedEmoji)),
            CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalseForNone()
    {
        // Act
        var result = _logger.IsEnabled(LogLevel.None);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    public void IsEnabled_ShouldReturnTrueForValidLevels(LogLevel level)
    {
        // Act
        var result = _logger.IsEnabled(level);

        // Assert
        Assert.True(result);
    }
    private static void ThrowTestException(string message)
    {
        throw new InvalidOperationException(message);
    }
}
