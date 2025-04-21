using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Trading.Common.Models;

namespace Trading.Application.Telegram.Handlers;

public class HelpCommandHandler : ICommandHandler
{
    private readonly ILogger<HelpCommandHandler> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    public static string Command => "/help";

    private readonly string _helpText = @"

*基础命令:*
/help \- 显示此帮助信息
/strategy \- [create\|delete\|pause\|resume] 策略管理
/alert \- [create\|delete\|empty\|pause\|resume] 警报相关

1\. *策略管理:*

*示例:*
创建策略:
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""PriceDropPercentage"":0.2,""Leverage"":5,""AccountType"":""Spot"",""StrategyType"":""BottomBuy""}`

删除策略:
`/strategy delete <Id>`

2\. *警报管理:*
创建警报\(支持间隔: 5m,15m,1h,4h,1d\):
`/alert create {""Symbol"":""BTCUSDT"",""Interval"":""4h"",""Expression"":""Math\.abs\(\(close \- open\) / open\) \>\= 0\.02""}`
`/alert create {""Symbol"":""BTCUSDT"",""Interval"":""4h"",""Expression"":""close \> 20000""}`

删除警报:
`/alert delete <Id>`

清空警报:
`/alert empty`";

    public HelpCommandHandler(ILogger<HelpCommandHandler> logger,
        ITelegramBotClient botClient,
        IOptions<TelegramSettings> settings)
    {
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;
    }

    public async Task HandleAsync(string parameters)
    {
        await _botClient.SendMessage(chatId: _chatId,
                                     text: _helpText,
                                     parseMode: ParseMode.MarkdownV2);
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
