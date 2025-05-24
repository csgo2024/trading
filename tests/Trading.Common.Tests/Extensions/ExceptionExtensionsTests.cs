using Trading.Common.Extensions;

namespace Trading.Common.Tests.Extensions;

public class ExceptionExtensionsTests
{
    [Fact]
    public void FlattenExceptions_ShouldReturnAllExceptions()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner exception");
        var exception = new InvalidOperationException("Outer exception", innerException);

        // Act
        var result = exception.FlattenExceptions().ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(exception, result);
        Assert.Contains(innerException, result);
    }

    [Fact]
    public void FlattenExceptions_WithAggregateException_ShouldReturnAllExceptions()
    {
        // Arrange
        var innerException1 = new InvalidOperationException("Inner exception 1");
        var innerException2 = new InvalidOperationException("Inner exception 2");
        var aggregateException = new AggregateException("Aggregate exception", innerException1, innerException2);

        // Act
        var result = aggregateException.FlattenExceptions().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(aggregateException, result);
        Assert.Contains(innerException1, result);
        Assert.Contains(innerException2, result);
    }
}
