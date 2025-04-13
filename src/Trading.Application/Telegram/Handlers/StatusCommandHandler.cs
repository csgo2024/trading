using System.Text;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class StatusCommandHandler : ICommandHandler
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IPriceAlertRepository _priceAlertRepository;

    private readonly ILogger<StatusCommandHandler> _logger;
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public static string Command => "/status";

    public StatusCommandHandler(
        IStrategyRepository strategyRepository,
        IPriceAlertRepository priceAlertRepository,
        ILogger<StatusCommandHandler> logger)
    {
        _strategyRepository = strategyRepository;
        _priceAlertRepository = priceAlertRepository;
        _logger = logger;
    }

    public async Task HandleAsync(string parameters)
    {
        var strategies = await _strategyRepository.GetAllStrategies();
        var alerts = await _priceAlertRepository.GetActiveAlertsAsync(default);
        var htmlBuilder = new StringBuilder();

        htmlBuilder.AppendLine("<pre>");
        foreach (var strategy in strategies)
        {
            var statusInfo = GetStatusInfo(strategy);
            htmlBuilder.AppendLine($"ID: {strategy.Id}");
            htmlBuilder.AppendLine($"{statusInfo.emoji} [{strategy.AccountType}-{strategy.Symbol}]: {statusInfo.status}");
            htmlBuilder.AppendLine($"跌幅: {strategy.PriceDropPercentage} / 目标价格: {strategy.TargetPrice} 💰");
            htmlBuilder.AppendLine($"金额: {strategy.Amount} / 数量: {strategy.Quantity}");

            if (strategy.UpdatedAt.HasValue)
            {
                var updatedTime = strategy.UpdatedAt.Value.AddHours(8).ToString(DateTimeFormat);
                htmlBuilder.AppendLine($"最后更新: {updatedTime} 🕒");
            }
            htmlBuilder.AppendLine("------------------------");
        }
        foreach (var alert in alerts)
        {
            htmlBuilder.AppendLine($"Symbol: {alert.Symbol}");
            htmlBuilder.AppendLine($"TargetPrice: {alert.TargetPrice}");
            htmlBuilder.AppendLine($"Alert Type: {alert.Type}");
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
