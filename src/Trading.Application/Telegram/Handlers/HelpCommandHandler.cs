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

    private const string HelpText = @"
📚 <b>命令帮助</b>
/help - 显示此帮助信息
/status - 查看所有策略状态
/create - 创建新策略 (需要JSON格式数据)
/resume - 恢复所有策略运行
/stop - 暂停所有策略运行
/delete - 删除指定的策略
/alarm - 警报相关
";

    private const string CreateStrategyText = @"
创建策略
```/create {""Symbol"":""BTCUSDT"",""Amount"":1000,""PriceDropPercentage"":0.2,""Leverage"":5,""AccountType"":""Spot"",""StrategyType"":""BottomBuy""}```
删除策略
```/delete 12345```
创建警报
```/alarm BTCUSDT Math.abs((close - open) / open) >= 0.02 ```
```/alarm empty``` \- 清空警报
";

    public HelpCommandHandler(ILogger<HelpCommandHandler> logger, ITelegramBotClient botClient, IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings), "TelegramSettings is not valid.");
    }

    public async Task HandleAsync(string parameters)
    {
        _logger.LogInformation(HelpText);
        await _botClient.SendMessage(
            chatId: _chatId,
            text: CreateStrategyText,
            parseMode: ParseMode.MarkdownV2
        );
    }

    public Task HandleCallbackAsync(string callbackData)
    {
        throw new NotImplementedException();
    }
}
