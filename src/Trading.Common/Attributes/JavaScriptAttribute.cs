using System.ComponentModel.DataAnnotations;
using Trading.Common.JavaScript;

namespace Trading.Common.Attributes;

public class JavaScriptAttribute : ValidationAttribute
{
    public bool Required { get; set; }
    private static readonly JavaScriptEvaluator _evaluator = new JavaScriptEvaluator(
        new Microsoft.Extensions.Logging.Abstractions.NullLogger<JavaScriptEvaluator>());

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string expression)
        {
            return Required
                ? new ValidationResult("JavaScript expression is required.")
                : ValidationResult.Success;
        }

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Required
                ? new ValidationResult("JavaScript expression cannot be empty.")
                : ValidationResult.Success;
        }

        if (!_evaluator.ValidateExpression(expression, out var errorMessage))
        {
            return new ValidationResult($"Invalid JavaScript expression: {errorMessage}");
        }

        return ValidationResult.Success;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The field {name} must be a valid JavaScript expression that uses only the allowed variables (open, close, high, low).";
    }
}
