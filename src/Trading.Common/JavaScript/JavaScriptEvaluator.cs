using Jint;
using Microsoft.Extensions.Logging;

namespace Trading.Common.JavaScript;

public class JavaScriptEvaluator : IDisposable
{
    private const int MaxRetries = 1;
    private const int RetryDelayMs = 100;
    private readonly Engine _jsEngine;
    private readonly ILogger<JavaScriptEvaluator> _logger;
    private readonly SemaphoreSlim _engineLock = new(1, 1);

    public JavaScriptEvaluator(ILogger<JavaScriptEvaluator> logger)
    {
        _logger = logger;
        _jsEngine = new Engine(cfg => cfg
            .LimitRecursion(10)
            .MaxStatements(50)
            .TimeoutInterval(TimeSpan.FromSeconds(30))
        );
    }

    public virtual bool ValidateExpression(string expression, out string message)
    {
        try
        {
            SetDefaultValues();
            _jsEngine.Evaluate(expression);
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public virtual bool EvaluateExpression(string expression,
                                  decimal open,
                                  decimal close,
                                  decimal high,
                                  decimal low)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _engineLock.Wait();
                try
                {
                    SetPriceValues(open, close, high, low);
                    var result = _jsEngine.Evaluate(expression);
                    return result.AsBoolean();
                }
                finally
                {
                    _engineLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "JavaScript evaluation error (attempt {Attempt}/{MaxRetries}) for expression: {Expression}",
                    attempt, MaxRetries, expression);

                if (attempt == MaxRetries)
                {
                    return false;
                }

                Thread.Sleep(RetryDelayMs * attempt); // Exponential backoff
            }
        }

        return false;
    }

    private void SetDefaultValues()
    {
        _jsEngine.SetValue("open", 0.0);
        _jsEngine.SetValue("close", 0.0);
        _jsEngine.SetValue("high", 0.0);
        _jsEngine.SetValue("low", 0.0);
    }

    private void SetPriceValues(decimal open, decimal close, decimal high, decimal low)
    {
        _jsEngine.SetValue("open", Convert.ToDouble(open));
        _jsEngine.SetValue("close", Convert.ToDouble(close));
        _jsEngine.SetValue("high", Convert.ToDouble(high));
        _jsEngine.SetValue("low", Convert.ToDouble(low));
    }

    public void Dispose()
    {
        _engineLock?.Dispose();
    }
}
