using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
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
*StrategyType* 类型说明

*BottomBuy、TopSell是基于RestClient的日内交易策略，第二天可以自动管理。*

做多策略\(现货\)
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.2,""Interval"":""1d"",""Leverage"":5,""AccountType"":""Spot"",""StrategyType"":""BottomBuy""}`

做空策略\(合约\)
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.2,""Interval"":""1d"",""Leverage"":5,""AccountType"":""Future"",""StrategyType"":""TopSell""}`

*CloseBuy、CloseSell是基于SocketClient的支持配置交易周期的策略，根据收盘价格进行下单！需要关注！！！。*

做空策略\(合约基于配置周期收盘价格\) WebSocket
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.0002,""Interval"":""4h"",""AccountType"":""Future"",""StrategyType"":""CloseSell""}`

做多策略\(合约基于配置周期收盘价格\) WebSocket
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.0002,""Interval"":""4h"",""AccountType"":""Future"",""StrategyType"":""CloseBuy""}`

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
        await _botClient.SendRequest(new SendMessageRequest
        {
            ChatId = _chatId,
            Text = _helpText,
            ParseMode = ParseMode.MarkdownV2,
            DisableNotification = true,
        }, CancellationToken.None);
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
