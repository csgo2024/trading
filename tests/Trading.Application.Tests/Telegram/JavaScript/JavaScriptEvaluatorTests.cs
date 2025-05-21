using Microsoft.Extensions.Logging;
using Moq;
using Trading.Common.JavaScript;

namespace Trading.Application.Tests.Telegram.JavaScript;

public class JavaScriptEvaluatorTests
{
    private readonly JavaScriptEvaluator _evaluator;
    private readonly Mock<ILogger<JavaScriptEvaluator>> _loggerMock;

    public JavaScriptEvaluatorTests()
    {
        _loggerMock = new Mock<ILogger<JavaScriptEvaluator>>();
        _evaluator = new JavaScriptEvaluator(_loggerMock.Object);
    }

    [Theory]
    [InlineData("close > open", true)]
    [InlineData("close < open", false)]
    [InlineData("high > low", true)]
    [InlineData("(high + low) / 2 > open", true)]
    public void EvaluateExpression_WithValidExpressions_ReturnsExpectedResult(string expression, bool expected)
    {
        // Arrange
        decimal open = 100m;
        decimal close = 110m;
        decimal high = 120m;
        decimal low = 90m;

        // Act
        var result = _evaluator.EvaluateExpression(expression, open, close, high, low);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EvaluateExpression_WithInvalidExpression_ReturnsFalse()
    {
        // Arrange
        string invalidExpression = "invalid * syntax >";

        // Act
        var result = _evaluator.EvaluateExpression(invalidExpression, 100m, 110m, 120m, 90m);

        // Assert
        Assert.False(result);
        _loggerMock.VerifyLoggingOnce(LogLevel.Error, "invalid * syntax >");
    }

    [Theory]
    [InlineData("close > open")]
    [InlineData("(high + low) / 2")]
    [InlineData("high >= low && close > open")]
    public void ValidateExpression_WithValidExpressions_ReturnsTrue(string expression)
    {
        // Act
        bool isValid = _evaluator.ValidateExpression(expression, out string message);

        // Assert
        Assert.True(isValid);
        Assert.Empty(message);
    }

    [Theory]
    [InlineData("invalid * syntax >")]
    [InlineData("close > ")]
    [InlineData("&& high")]
    public void ValidateExpression_WithInvalidExpressions_ReturnsFalse(string expression)
    {
        // Act
        bool isValid = _evaluator.ValidateExpression(expression, out string message);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(message);
    }

    [Fact]
    public async Task EvaluateExpression_WithConcurrentAccess_HandlesLockingCorrectly()
    {
        // Arrange
        const int concurrentTasks = 10;
        var tasks = new List<Task<bool>>();
        string expression = "close > open";

        // Act
        for (int i = 0; i < concurrentTasks; i++)
        {
            tasks.Add(Task.Run(() =>
                _evaluator.EvaluateExpression(expression, 100m, 110m, 120m, 90m)));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, Assert.True);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var evaluator = new JavaScriptEvaluator(_loggerMock.Object);

        // Act & Assert (should not throw)
        evaluator.Dispose();
        // Should handle multiple disposes gracefully
        evaluator.Dispose();
    }
}
