using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Telegram.Bot.Types.Enums;
using Trading.Application.Commands;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class AlertCommandHandlerTests
{
    private readonly Mock<ILogger<AlertCommandHandler>> _loggerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly AlertCommandHandler _handler;

    public AlertCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AlertCommandHandler>>();
        _mediatorMock = new Mock<IMediator>();
        _alertRepositoryMock = new Mock<IAlertRepository>();

        _handler = new AlertCommandHandler(
            _loggerMock.Object,
            _mediatorMock.Object,
            _alertRepositoryMock.Object);
    }

    [Theory]
    [InlineData(Status.Running, "Running")]
    [InlineData(Status.Paused, "Paused")]
    public async Task HandleAsync_WithEmptyParameters_ReturnAlertInformation(Status status, string displayText)
    {
        // arrange
        _alertRepositoryMock.Setup(x => x.GetAllAlerts())
            .ReturnsAsync([new Alert() { Symbol = "BTCUSDT", Status = status, Expression = "close > 100" }]);
        // Act
        await _handler.HandleAsync("");

        // Assert
        _loggerMock.Verify(
            x => x.BeginScope(
                It.Is<TelegramLoggerScope>(x => x.ParseMode == ParseMode.Html)),
            Times.Once);
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, displayText);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyParameters_ShouldLogInformation_WhenNoAlerts()
    {
        // arrange
        _alertRepositoryMock.Setup(x => x.GetAllAlerts())
            .ReturnsAsync([]);
        // Act
        await _handler.HandleAsync("");

        // Assert
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, "Alert is empty, please create and call later.");
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
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, "Alarms empty successfully");
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
        var alert = new Alert { Id = alertId, Status = Status.Running };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        await _handler.HandleAsync($"pause {alertId}");

        // Assert
        _alertRepositoryMock.Verify(x => x.UpdateAsync(
            alertId,
            It.Is<Alert>(a => a.Status == Status.Paused),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithResumeCommand_ResumesAlert()
    {
        // Arrange
        var alertId = "test-alert-id";
        var alert = new Alert { Id = alertId, Status = Status.Paused };

        _alertRepositoryMock
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        await _handler.HandleAsync($"resume {alertId}");

        // Assert
        _alertRepositoryMock.Verify(
            x => x.UpdateAsync(alert.Id, alert, It.IsAny<CancellationToken>()),
            Times.Once);
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
        _loggerMock.VerifyLoggingOnce(LogLevel.Error, $"Not found alarm: {alertId}");
    }

    [Theory]
    [InlineData("pause", Status.Running)]
    [InlineData("resume", Status.Paused)]
    public async Task HandleCallbackAsync_WithValidCallback_PauseOrResumeAlert(string action, Status status)
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
}
