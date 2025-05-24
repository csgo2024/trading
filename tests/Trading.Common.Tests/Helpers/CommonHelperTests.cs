using Trading.Common.Helpers;

namespace Trading.Common.Tests.Helpers;

public class CommonHelperTests
{

    [Theory]
    [InlineData(10.100000, 10.1)]
    [InlineData(10.000000, 10)]
    [InlineData(10.123000, 10.123)]
    [InlineData(10.120000, 10.12)]
    public void TrimEndZero_ShouldTrimCorrectly(decimal input, decimal expected)
    {
        // Act
        var result = CommonHelper.TrimEndZero(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
