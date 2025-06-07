using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Logging;

namespace Trading.Application.Telegram.Handlers;

public class HelpCommandHandler : ICommandHandler
{
    private readonly ILogger<HelpCommandHandler> _logger;
    public static string Command => "/help";
    public const string HelpText = """
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
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.2,
  "Interval": "1d",
  "AccountType": "Spot",
  "StopLossExpression": "low<=100000"
}`

2\. 合约做空策略 \(TopSell\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.2,
  "Interval": "1d",
  "AccountType": "Future",
  "StrategyType": "TopSell",
  "StopLossExpression": "high>=100000"
}`

3\. WebSocket合约做空策略 \(CloseSell\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.002,
  "Interval": "4h",
  "AccountType": "Future",
  "StrategyType": "CloseSell",
  "StopLossExpression": "close >= 100000"
}`

4\. WebSocket合约做多策略 \(CloseBuy\)
`/strategy create 
{
  "Symbol": "BTCUSDT",
  "Amount": 1000,
  "Volatility": 0.002,
  "Interval": "4h",
  "AccountType": "Future",
  "StrategyType": "CloseBuy",
  "StopLossExpression": "close <= 60000"
}`

删除策略:
`/strategy delete <Id>`

*警报管理*
创建警报\(支持间隔: 5m,15m,1h,4h,1d,3d,1w\):

1\. 价格波动警报
`/alert create 
{
  "Symbol":"BTCUSDT",
  "Interval":"4h",
  "Expression":"Math.abs((close-open)/open)>=0.02"
}`

2\. 价格阈值警报
`/alert create 
{
  "Symbol":"BTCUSDT",
  "Interval":"4h",
  "Expression":"close>=20000"
}`

删除警报:
`/alert delete <Id>`

清空警报:
`/alert empty`
""";

    public HelpCommandHandler(ILogger<HelpCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(string parameters)
    {
        var telegramScope = new TelegramLoggerScope
        {
            ParseMode = ParseMode.MarkdownV2,
        };

        using (_logger.BeginScope(telegramScope))
        {
#pragma warning disable CA2017 // Parameter count mismatch
            await Task.Run(() => _logger.LogInformation(HelpText));
#pragma warning restore CA2017 // Parameter count mismatch
        }
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
