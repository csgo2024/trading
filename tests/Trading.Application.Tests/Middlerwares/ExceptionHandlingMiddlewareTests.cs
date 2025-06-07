using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Middlerwares;

namespace Trading.Application.Tests.Middlerwares;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _mockLogger;
    private readonly Mock<IErrorMessageResolver> _mockErrorResolver;
    private readonly ExceptionHandlingMiddleware _middleware;

    public ExceptionHandlingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _mockErrorResolver = new Mock<IErrorMessageResolver>();
        _middleware = new ExceptionHandlingMiddleware(
            next: _ => throw new InvalidOperationException("Test exception"),
            logger: _mockLogger.Object,
            errorMessageResolver: _mockErrorResolver.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithCustomException_ShouldReturnCustomErrorResponse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var customException = new CustomException(100, "Custom error");
        var middleware = new ExceptionHandlingMiddleware(
            next: _ => throw customException,
            _mockLogger.Object,
            _mockErrorResolver.Object);

        _mockErrorResolver
            .Setup(x => x.ResolveAsync(100, "Custom error"))
            .ReturnsAsync("Resolved error message");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains("100", responseBody);
        Assert.Contains("Resolved error message", responseBody);
    }

    [Fact]
    public async Task InvokeAsync_WithNestedExceptions_ShouldLogAllExceptions()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var innerException = new InvalidOperationException("Inner exception");
        var outerException = new InvalidOperationException("Outer exception", innerException);
        var middleware = new ExceptionHandlingMiddleware(
            next: _ => throw outerException,
            _mockLogger.Object,
            _mockErrorResolver.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "Inner exception");
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "Outer exception");
    }

    [Fact]
    public async Task GetRequestInfo_WithRequestBody_ShouldCaptureBodyContent()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var bodyContent = "Test body content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(bodyContent));
        context.Request.Body = stream;
        context.Request.ContentType = "application/json";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next: _ => throw new InvalidOperationException("Test"),
            _mockLogger.Object,
            _mockErrorResolver.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, bodyContent);
    }

    [Fact]
    public async Task GetRequestInfo_WithQueryString_ShouldCaptureQueryParameters()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?param=value");
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            next: _ => throw new InvalidOperationException("Test"),
            _mockLogger.Object,
            _mockErrorResolver.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "?param=value");
    }
}

public class DefaultErrorMessageResolverTests
{
    [Fact]
    public async Task ResolveAsync_WithKnownErrorCode_ShouldReturnPredefinedMessage()
    {
        // Arrange
        var resolver = new DefaultErrorMessageResolver();

        // Act
        var result = await resolver.ResolveAsync(-1, "default message");

        // Assert
        Assert.Equal("System error.", result);
    }

    [Fact]
    public async Task ResolveAsync_WithUnknownErrorCode_ShouldReturnDefaultMessage()
    {
        // Arrange
        var resolver = new DefaultErrorMessageResolver();

        // Act
        var result = await resolver.ResolveAsync(999, "default message");

        // Assert
        Assert.Equal("default message", result);
    }
}
