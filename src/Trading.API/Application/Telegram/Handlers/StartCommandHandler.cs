using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Trading.Common.Models;

namespace Trading.API.Application.Telegram.Handlers;

public class StartCommandHandler : ICommandHandler
{
    private readonly ILogger<StartCommandHandler> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;

    public static string Command => "/start";

    private static string HelpText = @"
📚 <b>命令帮助</b>
/start - 显示此帮助信息
/status - 查看所有策略状态
/create - 创建新策略 (需要JSON格式数据)
/resume - 恢复所有策略运行
/stop - 暂停所有策略运行
/delete - 删除指定的策略
";

    private static string CreateStrategyText = @"
创建策略
```/create {""Symbol"":""BTCUSDT"",""Amount"":1000,""PriceDropPercentage"":0.2,""Leverage"":5,""AccountType"":""Spot"",""StrategyType"":""BuyBottom""}```
删除策略
```/delete 12345```";


    public StartCommandHandler(ILogger<StartCommandHandler> logger, ITelegramBotClient botClient, IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _botClient = botClient;
        _chatId = settings.Value.ChatId;
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
}