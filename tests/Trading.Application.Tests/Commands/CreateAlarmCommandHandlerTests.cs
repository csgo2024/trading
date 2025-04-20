using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Commands;
using Trading.Application.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class CreateAlarmCommandHandlerTests
{
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly CreateAlarmCommandHandler _handler;

    public CreateAlarmCommandHandlerTests()
    {
        _alarmRepositoryMock = new Mock<IAlarmRepository>();
        _mediatorMock = new Mock<IMediator>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _handler = new CreateAlarmCommandHandler(
            _alarmRepositoryMock.Object,
            _jsEvaluatorMock.Object,
            _mediatorMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAlarmAndPublishEvent()
    {
        // Arrange
        var command = new CreateAlarmCommand
        {
            Symbol = "btcusdt",
            Interval = "4h",
            Expression = "close > open"
        };

        _jsEvaluatorMock
            .Setup(x => x.ValidateExpression(command.Expression, out It.Ref<string>.IsAny))
            .Returns(true);

        Alarm? capturedAlarm = null;
        _alarmRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Alarm>(), It.IsAny<CancellationToken>()))
            .Callback<Alarm, CancellationToken>((alarm, _) => capturedAlarm = alarm)
            .ReturnsAsync((Alarm a, CancellationToken _) => a);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedAlarm);

        // Verify entity properties
        Assert.Equal(command.Symbol.ToUpper(), result.Symbol);
        Assert.Equal(command.Interval, result.Interval);
        Assert.Equal(command.Expression, result.Expression);
        Assert.True(result.IsActive);
        Assert.True(result.LastNotification <= DateTime.UtcNow);
        Assert.True(result.LastNotification > DateTime.UtcNow.AddMinutes(-1));

        // Verify repository call
        _alarmRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Alarm>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify event publication
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<AlarmCreatedEvent>(e => e.Alarm == result),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("", "4h", "close > open", "Symbol cannot be empty")]
    [InlineData("BTCUSDT", "", "close > open", "Interval cannot be empty")]
    [InlineData("BTCUSDT", "4h", "", "Expression cannot be empty")]
    [InlineData("BTCUSDT", "invalid", "close > open", "Invalid interval")]
    public async Task Handle_WithInvalidCommand_ShouldThrowValidationException(
        string symbol, string interval, string expression, string expectedError)
    {
        // Arrange
        var command = new CreateAlarmCommand
        {
            Symbol = symbol,
            Interval = interval,
            Expression = expression
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(expectedError, exception.Message);

        // Verify no repository calls or events
        _alarmRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Alarm>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlarmCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidJavaScriptExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateAlarmCommand
        {
            Symbol = "BTCUSDT",
            Interval = "4h",
            Expression = "invalid expression"
        };

        var errorMessage = "Invalid syntax";
        _jsEvaluatorMock
            .Setup(x => x.ValidateExpression(command.Expression, out It.Ref<string>.IsAny))
            .Returns(false)
            .Callback(new ValidateExpressionCallback((string _, out string message) =>
                message = errorMessage));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(errorMessage, exception.Message);

        // Verify no repository calls or events
        _alarmRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Alarm>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlarmCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ShouldNotPublishEvent()
    {
        // Arrange
        var command = new CreateAlarmCommand
        {
            Symbol = "BTCUSDT",
            Interval = "4h",
            Expression = "close > open"
        };

        _jsEvaluatorMock
            .Setup(x => x.ValidateExpression(command.Expression, out It.Ref<string>.IsAny))
            .Returns(true);

        _alarmRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Alarm>(), It.IsAny<CancellationToken>()))
            .Throws<InvalidOperationException>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Verify no events were published
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<AlarmCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

// Helper delegate for mocking out parameters
public delegate void ValidateExpressionCallback(string expression, out string message);
