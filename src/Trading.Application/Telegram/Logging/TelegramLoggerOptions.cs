using Microsoft.Extensions.Logging;

namespace Trading.Application.Telegram.Logging;

public class TelegramLoggerOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool IncludeScopes { get; set; } = true;
    public bool IncludeCategory { get; set; } = true;
    public int? EventId { get; set; }
    public List<string> ExcludeCategories { get; set; } = new();
}
