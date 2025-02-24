using Binance.Net.Objects.Models.Spot;
using Trading.API.Application.Helpers;

namespace Trading.API.Tests.Application.Helpers;

public class CommonHelperTests
{
    [Fact]
    public void AdjustPriceByStepSize_WhenFilterIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CommonHelper.AdjustPriceByStepSize(100m, null));

        Assert.Equal("filter", exception.ParamName);
    }

    [Theory]
    [InlineData(10.123456, 0.01, 10.12)]
    [InlineData(10.125, 0.01, 10.12)]
    [InlineData(10.155, 0.01, 10.15)]
    [InlineData(10.159, 0.01, 10.15)]
    public void AdjustPriceByStepSize_WithValidInput_ShouldRoundCorrectly(
        decimal price, decimal tickSize, decimal expected)
    {
        // Arrange
        var filter = new BinanceSymbolPriceFilter
        {
            TickSize = tickSize,
            MinPrice = 0.01m,
            MaxPrice = 100000m
        };

        // Act
        var result = CommonHelper.AdjustPriceByStepSize(price, filter);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.009, "Price must be greater than 0.01")]
    [InlineData(100001, "Price must be less than 100000")]
    public void AdjustPriceByStepSize_WithInvalidPrice_ShouldThrowException(
        decimal price, string expectedMessage)
    {
        // Arrange
        var filter = new BinanceSymbolPriceFilter
        {
            TickSize = 0.01m,
            MinPrice = 0.01m,
            MaxPrice = 100000m
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CommonHelper.AdjustPriceByStepSize(price, filter));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void AdjustQuantityBystepSize_WhenFilterIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CommonHelper.AdjustQuantityBystepSize(100m, null));

        Assert.Equal("filter", exception.ParamName);
    }

    [Theory]
    [InlineData(1.123456, 0.01, 1.12)]
    [InlineData(1.125, 0.01, 1.12)]
    [InlineData(1.155, 0.01, 1.15)]
    [InlineData(1.159, 0.01, 1.15)]
    public void AdjustQuantityBystepSize_WithValidInput_ShouldRoundCorrectly(
        decimal quantity, decimal stepSize, decimal expected)
    {
        // Arrange
        var filter = new BinanceSymbolLotSizeFilter
        {
            StepSize = stepSize,
            MinQuantity = 0.01m,
            MaxQuantity = 10000m
        };

        // Act
        var result = CommonHelper.AdjustQuantityBystepSize(quantity, filter);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.009, "Quantity must be greater than 0.01")]
    [InlineData(10001, "Quantity must be less than 10000")]
    public void AdjustQuantityBystepSize_WithInvalidQuantity_ShouldThrowException(
        decimal quantity, string expectedMessage)
    {
        // Arrange
        var filter = new BinanceSymbolLotSizeFilter
        {
            StepSize = 0.01m,
            MinQuantity = 0.01m,
            MaxQuantity = 10000m
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CommonHelper.AdjustQuantityBystepSize(quantity, filter));
        Assert.Equal(expectedMessage, exception.Message);
    }

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
