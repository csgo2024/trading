using System.Text;
using System.Text.Json;
using System.Net;
using Trading.API.Application.Extensions;

namespace Trading.API.Application.Middlerwares;

public class CustomException : Exception
{
    public int ErrorCode { get; }

    // 构造函数只接收 ErrorCode
    public CustomException(int errorCode)
        : base($"Error occurred with code: {errorCode}")
    {
        ErrorCode = errorCode;
    }

    // 构造函数接收 ErrorCode 和自定义消息
    public CustomException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    // 构造函数接收 ErrorCode、自定义消息和内部异常
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

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
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

        // 记录请求信息
        var requestInfo = await GetRequestInfoAsync(context.Request);

        // 获取所有嵌套异常
        var exceptions = exception.FlattenExceptions().ToList();

        // 记录所有异常信息
        var exceptionMessages = exceptions.Select(ex => ex.Message);
        _logger.LogError(
            "Request: {RequestInfo}\nExceptions: {ExceptionMessages}",
            requestInfo,
            string.Join("\n", exceptionMessages)
        );

        // 获取自定义异常信息
        var (errorCode, errorMessage) = GetErrorDetails(exceptions);

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            ErrorCode = errorCode,
            ErrorMessage = await _errorMessageResolver.ResolveAsync(errorCode, errorMessage)
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task<string> GetRequestInfoAsync(HttpRequest request)
    {
        var bodyText = string.Empty;

        // 确保可以重复读取Request.Body
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
            request.Body.Position = 0;  // 重置位置以供后续中间件读取
        }

        var queryString = request.QueryString.HasValue
            ? request.QueryString.Value
            : string.Empty;

        return $"Method: {request.Method}, " +
               $"Path: {request.Path}, " +
               $"QueryString: {queryString}, " +
               $"Body: {bodyText}";
    }

    private (int errorCode, string ErrorMessage) GetErrorDetails(IEnumerable<Exception> exceptions)
    {
        // 优先获取CustomException
        var customException = exceptions.OfType<CustomException>().FirstOrDefault();
        if (customException != null)
        {
            return (customException.ErrorCode, customException.Message);
        }

        // 如果没有CustomException，返回通用错误
        return (-1, "An unexpected error occurred.");
    }
}

// 定义错误消息解析器接口
public interface IErrorMessageResolver
{
    Task<string> ResolveAsync(int errorCode, string defaultMessage);
}

// 错误消息解析器的示例实现
public class DefaultErrorMessageResolver : IErrorMessageResolver
{
    private readonly Dictionary<int, string> _errorMessages;

    public DefaultErrorMessageResolver()
    {
        _errorMessages = new Dictionary<int, string>
        {
            // 在这里定义错误代码对应的消息
            { -1, "系统错误" },
            // 添加更多错误码和对应消息...
        };
    }

    public Task<string> ResolveAsync(int errorCode, string defaultMessage)
    {
        return Task.FromResult(_errorMessages.TryGetValue(errorCode, out string message)
            ? message
            : defaultMessage);
    }
}

// 中间件扩展方法
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}