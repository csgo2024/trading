using Trading.API.Extensions;

namespace Trading.API.Tests;

public class ExceptionExtensionsTests : IClassFixture<TradingApiFixture>
{
    private readonly TradingApiFixture _fixture;

    public ExceptionExtensionsTests(TradingApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void FlattenExceptions_ShouldReturnAllExceptions()
    {
        // Arrange
        var innerException = new Exception("Inner exception");
        var exception = new Exception("Outer exception", innerException);

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
        var innerException1 = new Exception("Inner exception 1");
        var innerException2 = new Exception("Inner exception 2");
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