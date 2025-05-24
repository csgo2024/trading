using System.ComponentModel.DataAnnotations;
using Moq;
using Trading.Application.Commands;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

public class CreateAlertCommandHandlerTests
{
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly CreateAlertCommandHandler _handler;

    public CreateAlertCommandHandlerTests()
    {
        _alertRepositoryMock = new Mock<IAlertRepository>();
        _handler = new CreateAlertCommandHandler(_alertRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAlertAndPublishEvent()
    {
        // Arrange
        var command = new CreateAlertCommand
        {
            Symbol = "btcusdt",
            Interval = "4h",
            Expression = "close > open"
        };

        Alert? capturedAlert = null;
        _alertRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((alert, _) => capturedAlert = alert)
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedAlert);

        // Verify entity properties
        Assert.Equal(command.Symbol.ToUpper(), result.Symbol);
        Assert.Equal(command.Interval, result.Interval);
        // Assert.Equal(command.Expression, result.Expression);
        Assert.True(result.Status == Status.Running);
        Assert.True(result.LastNotification <= DateTime.UtcNow);
        Assert.True(result.LastNotification > DateTime.UtcNow.AddMinutes(-1));

        // Verify repository call
        _alertRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("", "4h", "close > open", "Symbol cannot be empty")]
    [InlineData("BTCUSDT", "", "close > open", "Interval is required.")]
    [InlineData("BTCUSDT", "4h", "", "JavaScript expression cannot be empty.")]
    [InlineData("BTCUSDT", "invalid", "close > open", "Invalid interval")]
    public async Task Handle_WithInvalidCommand_ShouldThrowValidationException(
        string symbol, string interval, string expression, string expectedError)
    {
        // Arrange
        var command = new CreateAlertCommand
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
        _alertRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidJavaScriptExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateAlertCommand
        {
            Symbol = "BTCUSDT",
            Interval = "4h",
            Expression = "invalid expression"
        };

        var errorMessage = "Invalid JavaScript expression";
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(errorMessage, exception.Message);

        // Verify no repository calls or events
        _alertRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ShouldNotPublishEvent()
    {
        // Arrange
        var command = new CreateAlertCommand
        {
            Symbol = "BTCUSDT",
            Interval = "4h",
            Expression = "close > open"
        };

        _alertRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Throws<InvalidOperationException>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

    }
}
