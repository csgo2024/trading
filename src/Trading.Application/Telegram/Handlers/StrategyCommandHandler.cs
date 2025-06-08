using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Commands;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class StrategyCommandHandler : ICommandHandler
{
    private readonly ILogger<StrategyCommandHandler> _logger;
    private readonly IMediator _mediator;
    private readonly IStrategyRepository _strategyRepository;
    public static string Command => "/strategy";
    public static string CallbackPrefix => "strategy";

    public StrategyCommandHandler(IMediator mediator,
                                  ILogger<StrategyCommandHandler> logger,
                                  IStrategyRepository strategyRepository)
    {
        _mediator = mediator;
        _logger = logger;
        _strategyRepository = strategyRepository;
    }

    public async Task HandleAsync(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            await HandleDefault();
            return;
        }

        var parts = parameters.Trim().Split([' '], 2);
        var subCommand = parts[0].ToLower();
        var subParameters = parts.Length > 1 ? parts[1] : string.Empty;

        switch (subCommand)
        {
            case "create":
                await HandleCreate(subParameters);
                break;
            case "delete":
                await HandleDelete(subParameters);
                break;
            case "pause":
                await HandlPause(subParameters);
                break;
            case "resume":
                await HandleResume(subParameters);
                break;
            default:
                _logger.LogError("Unknown command. Use: create, delete, pause, or resume");
                break;
        }
    }

    private async Task HandleDefault()
    {
        var strategies = await _strategyRepository.GetAllStrategies();
        if (strategies.Count == 0)
        {
            _logger.LogInformation("Strategy is empty, please create and call later.");
            return;
        }

        foreach (var strategy in strategies)
        {
            var (emoji, status) = strategy.Status.GetStatusInfo();
            var text = $"""
            {emoji} [{strategy.AccountType}-{strategy.StrategyType}-{strategy.Symbol}]: {status}
            Internal: {strategy.Interval} / OpenPrice: {strategy.OpenPrice}
            TargetPrice: {strategy.TargetPrice} 💰
            Volatility: {strategy.Volatility:P2} 
            Amount: {strategy.Amount} / Quantity: {strategy.Quantity}
            """;
            var buttons = strategy.Status switch
            {
                Status.Running => [InlineKeyboardButton.WithCallbackData("⏸️ Pause", $"strategy_pause_{strategy.Id}")],
                Status.Paused => new[] { InlineKeyboardButton.WithCallbackData("▶️ Resume", $"strategy_resume_{strategy.Id}") },
                _ => throw new InvalidOperationException()
            };
            buttons = [.. buttons, InlineKeyboardButton.WithCallbackData("🗑️ Delete", $"strategy_delete_{strategy.Id}")];
            var telegramScope = new TelegramLoggerScope
            {
                Title = "📊 Strategy Status",
                ReplyMarkup = new InlineKeyboardMarkup([buttons])
            };

            using (_logger.BeginScope(telegramScope))
            {
                _logger.LogInformation(text);
            }
        }

    }
    private async Task HandleCreate(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentNullException(nameof(json));
        }

        var command = JsonConvert.DeserializeObject<CreateStrategyCommand>(json) ?? throw new InvalidOperationException("Failed to parse strategy parameters");
        await _mediator.Send(command);
    }

    private async Task HandleDelete(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id), "Strategy ID cannot be null or empty");
        }

        var command = new DeleteStrategyCommand { Id = id.Trim() };
        var result = await _mediator.Send(command);

        if (!result)
        {
            throw new InvalidOperationException($"Failed to delete strategy {id}");
        }
        _logger.LogInformation("Strategy {id} deleted successfully.", id);
    }

    private async Task HandlPause(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var strategy = await _strategyRepository.GetByIdAsync(id);
        if (strategy == null)
        {
            _logger.LogError("Not found strategy: {Id}", id);
            return;
        }
        strategy.Pause();
        await _strategyRepository.UpdateAsync(id, strategy);
        _logger.LogInformation("Strategy {id} paused successfully.", id);
    }

    private async Task HandleResume(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var strategy = await _strategyRepository.GetByIdAsync(id);
        if (strategy == null)
        {
            _logger.LogError("Not found strategy: {Id}", id);
            return;
        }
        strategy.Resume();
        await _strategyRepository.UpdateAsync(id, strategy);
        _logger.LogInformation("Strategy {id} resumed successfully.", id);
    }

    public async Task HandleCallbackAsync(string action, string parameters)
    {
        var strategyId = parameters.Trim();
        switch (action)
        {
            case "pause":
                await HandlPause(strategyId);
                break;

            case "resume":
                await HandleResume(strategyId);
                break;

            case "delete":
                await HandleDelete(strategyId);
                break;
        }
    }
}
