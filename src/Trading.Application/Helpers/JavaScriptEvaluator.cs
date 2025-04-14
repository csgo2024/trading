using Jint;
using Microsoft.Extensions.Logging;

namespace Trading.Common.Tools;

public class JavaScriptEvaluator : IDisposable
{
    private readonly ILogger<JavaScriptEvaluator> _logger;
    private readonly Engine _jsEngine;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private const int MaxRetries = 1;
    private const int RetryDelayMs = 100;

    public JavaScriptEvaluator(ILogger<JavaScriptEvaluator> logger)
    {
        _logger = logger;
        _jsEngine = new Engine(cfg => cfg
            .LimitRecursion(10)
            .MaxStatements(50)
            .TimeoutInterval(TimeSpan.FromSeconds(30))
        );
    }

    public bool ValidateCondition(string condition, out string message)
    {
        try
        {
            SetDefaultValues();
            _jsEngine.Evaluate(condition);
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public bool EvaluateCondition(string condition,
                                  decimal open,
                                  decimal close,
                                  decimal high,
                                  decimal low)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _engineLock.Wait();
                try
                {
                    SetPriceValues(open, close, high, low);
                    var result = _jsEngine.Evaluate(condition);
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
                    "JavaScript evaluation error (attempt {Attempt}/{MaxRetries}) for condition: {Condition}",
                    attempt, MaxRetries, condition);

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
