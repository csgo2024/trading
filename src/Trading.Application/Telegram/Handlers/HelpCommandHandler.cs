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

*策略管理*

*策略类型说明*

1\. RestClient策略
\- *BottomBuy* 和 *TopSell*: 基于当天开盘价格执行的策略
\- 特点：不需要等待收盘，第二天自动管理

2\. WebSocket策略
\- *CloseBuy* 和 *CloseSell*: 基于指定周期收盘价格执行的策略
\- ⚠️ 注意：必须等待当前周期收盘后才会执行下单

*策略示例*

1\. 现货做多策略 \(BottomBuy\)
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.2,""Interval"":""1d"",""Leverage"":5,""AccountType"":""Spot"",""StrategyType"":""BottomBuy""}`

2\. 合约做空策略 \(TopSell\)
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.2,""Interval"":""1d"",""Leverage"":5,""AccountType"":""Future"",""StrategyType"":""TopSell""}`

3\. WebSocket合约做空策略 \(CloseSell\)
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.002,""Interval"":""4h"",""AccountType"":""Future"",""StrategyType"":""CloseSell""}`

4\. WebSocket合约做多策略 \(CloseBuy\)
`/strategy create {""Symbol"":""BTCUSDT"",""Amount"":1000,""Volatility"":0.002,""Interval"":""4h"",""AccountType"":""Future"",""StrategyType"":""CloseBuy""}`

删除策略:
`/strategy delete <Id>`

*警报管理*
创建警报\(支持间隔: 5m,15m,1h,4h,1d\):

1\. 价格波动警报
`/alert create {""Symbol"":""BTCUSDT"",""Interval"":""4h"",""Expression"":""Math\.abs\(\(close \- open\) / open\) \>\= 0\.02""}`

2\. 价格阈值警报
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
