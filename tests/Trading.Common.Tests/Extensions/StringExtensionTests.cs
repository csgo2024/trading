using Trading.Common.Extensions;

namespace Trading.Common.Tests.Extensions;

public class StringExtensionTests
{
    [Theory]
    [InlineData("FooBar", "foo_bar")]
    [InlineData("fooBar", "foo_bar")]
    [InlineData("FBBaz", "fb_baz")]
    [InlineData("Foo", "foo")]
    [InlineData("Fo", "fo")]
    [InlineData("fO", "f_o")]
    [InlineData("OO", "oo")]
    [InlineData("F", "f")]
    [InlineData("", "")]
    public void StringExtensions_ToSnakeCase_Converts_Correctly(string input, string expected)
    {
        Assert.Equal(expected, input.ToSnakeCase());
    }

}
