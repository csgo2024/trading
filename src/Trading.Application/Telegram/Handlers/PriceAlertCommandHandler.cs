using Jint;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class PriceAlertCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<PriceAlertCommandHandler> _logger;
    private readonly string _chatId;
    private readonly IPriceAlertRepository _alertRepository;
    private readonly Engine _jsEngine;
    private readonly IMediator _mediator;
    public static string Command => "/alert";

    public PriceAlertCommandHandler(
        ITelegramBotClient botClient,
        ILogger<PriceAlertCommandHandler> logger,
        IOptions<TelegramSettings> settings,
        IMediator mediator,
        IPriceAlertRepository alertRepository)
    {
        _botClient = botClient;
        _logger = logger;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _alertRepository = alertRepository;
        _mediator = mediator;
        _jsEngine = new Engine(cfg => cfg
            .LimitRecursion(10)
            .MaxStatements(50)
            .TimeoutInterval(TimeSpan.FromSeconds(10))
        );
    }

    public async Task HandleAsync(string parameters)
    {
        try
        {
            var parts = parameters.Trim().Split([' '], 2);
            if (parts.Length != 2)
            {
                await _botClient.SendMessage(
                    chatId: _chatId,
                    text: "<pre>格式错误! 正确格式:\n/alert BTCUSDT close > 50000</pre>",
                    parseMode: ParseMode.Html
                );
                return;
            }

            var symbol = parts[0].ToUpper();
            var condition = parts[1];

            // 验证JavaScript条件
            try
            {
                _jsEngine.SetValue("open", 0.0);
                _jsEngine.SetValue("close", 0.0);
                _jsEngine.SetValue("high", 0.0);
                _jsEngine.SetValue("low", 0.0);
                _jsEngine.Evaluate(condition);
            }
            catch (Exception ex)
            {
                await _botClient.SendMessage(
                    chatId: _chatId,
                    text: $"<pre>条件语法错误: {ex.Message}</pre>",
                    parseMode: ParseMode.Html
                );
                return;
            }

            var alert = new PriceAlert
            {
                Symbol = symbol,
                Condition = condition,
                IsActive = true,
                LastNotification = DateTime.UtcNow,
            };

            await _alertRepository.AddAsync(alert);

            await _botClient.SendMessage(
                chatId: _chatId,
                text: $"<pre>已设置 {symbol} 价格预警\n条件: {condition}</pre>",
                parseMode: ParseMode.Html
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create alert failed");
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
                    await _mediator.Publish(new AlertStatusChangedEvent(alertId, false));
                    break;

                case "resume":
                    alert.IsActive = true;
                    await _mediator.Publish(new AlertStatusChangedEvent(alertId, true));
                    await _alertRepository.UpdateAsync(alertId, alert);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理价格报警回调失败");
        }
    }
}
