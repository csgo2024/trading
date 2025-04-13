using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class PriceAlertCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<PriceAlertCommandHandler> _logger;
    private readonly string _chatId;
    private readonly IPriceAlertRepository _alertRepository;

    public static string Command => "/alert";

    public PriceAlertCommandHandler(
        ITelegramBotClient botClient,
        ILogger<PriceAlertCommandHandler> logger,
        IOptions<TelegramSettings> settings,
        IPriceAlertRepository alertRepository)
    {
        _botClient = botClient;
        _logger = logger;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _alertRepository = alertRepository;
    }

    public async Task HandleAsync(string parameters)
    {
        try
        {
            var parts = parameters.Split(' ');
            if (parts.Length != 3)
            {
                await _botClient.SendMessage(
                    chatId: _chatId,
                    text: "<pre>格式错误! 正确格式: /alert BTCUSDT above/below 50000</pre>",
                    parseMode: ParseMode.Html
                );
                return;
            }

            var symbol = parts[0].ToUpper();
            var type = parts[1].ToLower() == "above" ? AlertType.Above : AlertType.Below;
            if (!decimal.TryParse(parts[2], out decimal targetPrice))
            {
                await _botClient.SendMessage(
                    chatId: _chatId,
                    text: "<pre>价格格式错误!</pre>",
                    parseMode: ParseMode.Html
                );
                return;
            }

            var alert = new PriceAlert
            {
                Symbol = symbol,
                TargetPrice = targetPrice,
                Type = type,
                LastNotification = DateTime.UtcNow,
                IsActive = true
            };

            await _alertRepository.AddAsync(alert);

            await _botClient.SendMessage(
                chatId: _chatId,
                text: $"<pre>已设置 {symbol} 价格预警: {(type == AlertType.Above ? "高于" : "低于")} {targetPrice}</pre>",
                parseMode: ParseMode.Html
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "<pre>设置价格预警失败</pre>");
        }
    }

    public async Task HandleCallbackAsync(string callbackData)
    {
        try
        {
            var parts = callbackData.Split('_');
            var action = parts[0];
            var alertId = parts[1];
            var alert = await _alertRepository.GetByIdAsync(alertId);
            switch (action)
            {
                case "pause":
                    alert.IsActive = false;
                    await _alertRepository.UpdateAsync(alertId, alert);
                    break;

                case "resume":
                    alert.IsActive = true;
                    await _alertRepository.UpdateAsync(alertId, alert);
                    break;
            }
            _logger.LogInformation("已{Action}价格报警", action == "pause" ? "暂停" : "恢复");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理价格报警回调失败");
        }
    }
}
