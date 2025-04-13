using Jint;
using Microsoft.Extensions.Logging;

namespace Trading.Common.Tools;

public class JavaScriptEvaluator
{
    private readonly ILogger<JavaScriptEvaluator> _logger;
    private readonly Engine _jsEngine;

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
        try
        {
            SetPriceValues(open, close, high, low);
            var result = _jsEngine.Evaluate(condition);
            return result.AsBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JavaScript evaluation error for condition: {Condition}", condition);
            return false;
        }
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
        _jsEngine.SetValue("open", (double)open);
        _jsEngine.SetValue("close", (double)close);
        _jsEngine.SetValue("high", (double)high);
        _jsEngine.SetValue("low", (double)low);
    }
}
