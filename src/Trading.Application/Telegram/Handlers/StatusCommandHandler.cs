using System.Text;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Alarms;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class StatusCommandHandler : ICommandHandler
{
    private readonly IStrategyRepository _strategyRepository;

    private readonly AlarmNotificationService _alarmNotificationService;

    private readonly ILogger<StatusCommandHandler> _logger;
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public static string Command => "/status";

    public StatusCommandHandler(
        IStrategyRepository strategyRepository,
        AlarmNotificationService alarmNotificationService,
        ILogger<StatusCommandHandler> logger)
    {
        _strategyRepository = strategyRepository;
        _alarmNotificationService = alarmNotificationService;
        _logger = logger;
    }

    public async Task HandleAsync(string parameters)
    {
        var strategies = await _strategyRepository.GetAllStrategies();
        var alarms = _alarmNotificationService.GetActiveAlarms();
        var htmlBuilder = new StringBuilder();

        htmlBuilder.AppendLine("<pre>");
        foreach (var strategy in strategies)
        {
            var (emoji, status) = GetStatusInfo(strategy);
            htmlBuilder.AppendLine($"ID: {strategy.Id}");
            htmlBuilder.AppendLine($"{emoji} [{strategy.AccountType}-{strategy.Symbol}]: {status}");
            htmlBuilder.AppendLine($"跌幅: {strategy.PriceDropPercentage} / 目标价格: {strategy.TargetPrice} 💰");
            htmlBuilder.AppendLine($"金额: {strategy.Amount} / 数量: {strategy.Quantity}");

            if (strategy.UpdatedAt.HasValue)
            {
                var updatedTime = strategy.UpdatedAt.Value.AddHours(8).ToString(DateTimeFormat);
                htmlBuilder.AppendLine($"最后更新: {updatedTime} 🕒");
            }
            htmlBuilder.AppendLine("------------------------");
        }
        foreach (var alarm in alarms)
        {
            var safeMessage = alarm.Expression.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            htmlBuilder.AppendLine($"{alarm.Symbol} {alarm.Interval} {safeMessage}");
            htmlBuilder.AppendLine("------------------------");
        }
        htmlBuilder.AppendLine("</pre>");

        _logger.LogInformation(htmlBuilder.ToString());
    }

    private static (string emoji, string status) GetStatusInfo(Strategy strategy) => strategy.Status switch
    {
        StateStatus.Running => ("🟢", "运行中"),
        StateStatus.Paused => ("🔴", "已暂停"),
        _ => ("⚠️", "未知状态")
    };

    public Task HandleCallbackAsync(string callbackData)
    {
        throw new NotImplementedException();
    }
}
