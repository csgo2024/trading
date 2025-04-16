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
/status \- 查看所有策略状态
/create \- 创建新策略
/resume \- 恢复所有策略运行
/stop \- 暂停所有策略运行
/delete \- 删除指定的策略
/alarm \- 警报相关

1\. *创建策略:*
`/create {""Symbol"":""BTCUSDT"",""Amount"":1000,""PriceDropPercentage"":0.2,""Leverage"":5,""AccountType"":""Spot"",""StrategyType"":""BottomBuy""}`

2\. *删除策略:*
`/delete 12345`

3\. *创建警报\(支持间隔: 5m,15m,1h,4h,1d\):*
`/alarm BTCUSDT 1h Math\.abs\(\(close \- open\) / open\) \>\= 0\.02`

4\. *清空警报:*
`/alarm empty`";

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

    public Task HandleCallbackAsync(string callbackData)
    {
        throw new NotImplementedException();
    }
}
