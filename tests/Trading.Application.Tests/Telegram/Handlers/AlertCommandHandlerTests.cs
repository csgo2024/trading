using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Trading.Application.Commands;
using Trading.Application.Telegram.Handlers;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class AlertCommandHandlerTests
{
    private readonly Mock<ILogger<AlertCommandHandler>> _loggerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ITelegramBotClient> _botClientMock;
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly AlertCommandHandler _handler;
    private readonly string _testChatId = "456456481";

    public AlertCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AlertCommandHandler>>();
        _mediatorMock = new Mock<IMediator>();
        _alertRepositoryMock = new Mock<IAlertRepository>();
        _botClientMock = new Mock<ITelegramBotClient>();
        var settings = new TelegramSettings { ChatId = _testChatId };
        var optionsMock = new Mock<IOptions<TelegramSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settings);

        _handler = new AlertCommandHandler(
            _loggerMock.Object,
            _mediatorMock.Object,
            _alertRepositoryMock.Object,
            _botClientMock.Object,
            optionsMock.Object);
    }

    [Theory]
    [InlineData(StateStatus.Running, "运行中")]
    [InlineData(StateStatus.Paused, "已暂停")]
    public async Task HandleAsync_WithEmptyParameters_ReturnAlertInformation(StateStatus status, string displayText)
    {
        // arrange
        _alertRepositoryMock.Setup(x => x.GetAllAlerts())
            .ReturnsAsync([new Alert() { Symbol = "BTCUSDT", Status = status, Expression = "close > 100" }]);
        _botClientMock
            .Setup(x => x.SendRequest(It.IsAny<SendMessageRequest>(), default))
            .ReturnsAsync(new Message());
        // Act
        await _handler.HandleAsync("");

        // Assert
        _botClientMock.Verify(x => x.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.ChatId == _testChatId &&
                r.Text.Contains(displayText) &&
                r.ParseMode == ParseMode.Html),
            default),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyCommand_ClearsAllAlerts()
    {
        // Arrange
        _alertRepositoryMock
            .Setup(x => x.ClearAllAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        await _handler.HandleAsync("empty");

        // Assert
        _alertRepositoryMock.Verify(x => x.ClearAllAlertsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(x => x.Publish(It.IsAny<AlertEmptyedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyLogInfo("已清空所有价格警报");
    }

    [Fact]
    public async Task HandleAsync_WithCreateCommand_CreatesAlert()
    {
        // Arrange
        var alertJson = """{"Symbol":"BTCUSDT","Expression":"close > 1000","Interval":"1h"}""";
        var command = new CreateAlertCommand { Symbol = "BTCUSDT", Expression = "close > 1000", Interval = "1h" };

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<CreateAlertCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Alert());

        // Act
        await _handler.HandleAsync($"create {alertJson}");

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<CreateAlertCommand>(c =>
                c.Symbol == command.Symbol &&
                c.Expression == command.Expression &&
                c.Interval == command.Interval),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCreateJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "invalid json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonReaderException>(() =>
            _handler.HandleAsync($"create {invalidJson}"));
    }

    [Fact]
    public async Task HandleAsync_WithDeleteCommand_DeletesAlert()
    {
        // Arrange
        var alertId = "test-alert-id";
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<DeleteAlertCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"delete {alertId}");

        // Assert
        _mediatorMock.Verify(x => x.Send(
            It.Is<DeleteAlertCommand>(c => c.Id == alertId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDeleteCommandFails_ThrowsException()
    {
        // Arrange
        var alertId = "test-alert-id";
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<DeleteAlertCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync($"delete {alertId}"));
        Assert.Contains(alertId, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithPauseCommand_PausesAlert()
    {
        // Arrange
        var alertId = "test-alert-id";
        var alert = new Alert { Id = alertId, Status = StateStatus.Running };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        await _handler.HandleAsync($"pause {alertId}");

        // Assert
        _alertRepositoryMock.Verify(x => x.UpdateAsync(
            alertId,
            It.Is<Alert>(a => a.Status == StateStatus.Paused),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(x => x.Publish(
            It.Is<AlertPausedEvent>(e => e.AlertId == alertId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithResumeCommand_ResumesAlert()
    {
        // Arrange
        var alertId = "test-alert-id";
        var alert = new Alert { Id = alertId, Status = StateStatus.Paused };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        await _handler.HandleAsync($"resume {alertId}");

        // Assert
        _alertRepositoryMock.Verify(
            x => x.UpdateAsync(alert.Id, alert, It.IsAny<CancellationToken>()),
            Times.Once);
        _mediatorMock.Verify(x => x.Publish(
            It.Is<AlertResumedEvent>(e => e.Alert == alert),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    public async Task HandleAsync_WithNonexistentAlert_LogsError(string command)
    {
        // Arrange
        var alertId = "nonexistent-id";
        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as Alert);

        // Act
        await _handler.HandleAsync($"{command} {alertId}");

        // Assert
        VerifyLogError($"未找到报警 ID: {alertId}");
    }

    [Theory]
    [InlineData("pause", StateStatus.Running)]
    [InlineData("resume", StateStatus.Paused)]
    public async Task HandleCallbackAsync_WithValidCallback_PauseOrResumeAlert(string action, StateStatus status)
    {
        // Arrange
        var alertId = "test-alert-id";
        var alert = new Alert { Id = alertId, Status = status };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        await _handler.HandleCallbackAsync(action, alertId);

        // Assert
        _alertRepositoryMock.Verify(x => x.UpdateAsync(
            alertId,
            It.Is<Alert>(a => a.Status != status),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyLogError(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    private void VerifyLogInfo(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
