using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Trading.Common.Extensions;

namespace Trading.Application.Middlerwares;

public class CustomException : Exception
{
    public int ErrorCode { get; }

    public CustomException(int errorCode)
        : base($"Error occurred with code: {errorCode}")
    {
        ErrorCode = errorCode;
    }

    public CustomException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public CustomException(int errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IErrorMessageResolver _errorMessageResolver;

    public ExceptionHandlingMiddleware(RequestDelegate next,
                                       ILogger<ExceptionHandlingMiddleware> logger,
                                       IErrorMessageResolver errorMessageResolver)
    {
        _next = next;
        _logger = logger;
        _errorMessageResolver = errorMessageResolver;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var requestInfo = await GetRequestInfoAsync(context.Request);

        var exceptions = exception.FlattenExceptions().ToList();

        var exceptionMessages = exceptions.Select(ex => ex.Message);
        _logger.LogError(
            "Request: {RequestInfo}\nExceptions: {ExceptionMessages}",
            requestInfo,
            string.Join("\n", exceptionMessages)
        );

        var (errorCode, errorMessage) = GetErrorDetails(exceptions);

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            ErrorCode = errorCode,
            ErrorMessage = await _errorMessageResolver.ResolveAsync(errorCode, errorMessage)
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static async Task<string> GetRequestInfoAsync(HttpRequest request)
    {
        var bodyText = string.Empty;

        request.EnableBuffering();

        if (request.Body.CanRead)
        {
            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            bodyText = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        var queryString = request.QueryString.HasValue
            ? request.QueryString.Value
            : string.Empty;

        return $"Method: {request.Method}, " +
               $"Path: {request.Path}, " +
               $"QueryString: {queryString}, " +
               $"Body: {bodyText}";
    }

    private static (int errorCode, string ErrorMessage) GetErrorDetails(IEnumerable<Exception> exceptions)
    {
        var customException = exceptions.OfType<CustomException>().FirstOrDefault();
        if (customException != null)
        {
            return (customException.ErrorCode, customException.Message);
        }

        return (-1, "An unexpected error occurred.");
    }
}

public interface IErrorMessageResolver
{
    Task<string> ResolveAsync(int errorCode, string defaultMessage);
}

public class DefaultErrorMessageResolver : IErrorMessageResolver
{
    private readonly Dictionary<int, string> _errorMessages;

    public DefaultErrorMessageResolver()
    {
        _errorMessages = new Dictionary<int, string>
        {
            { -1, "System error." },
        };
    }

    public Task<string> ResolveAsync(int errorCode, string defaultMessage)
    {
        return Task.FromResult(_errorMessages.TryGetValue(errorCode, out var message)
            ? message
            : defaultMessage);
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
