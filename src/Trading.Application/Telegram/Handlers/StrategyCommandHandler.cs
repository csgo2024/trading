using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Trading.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class StrategyCommandHandler : ICommandHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<StrategyCommandHandler> _logger;
    private readonly IStrategyRepository _strategyRepository;

    public static string Command => "/strategy";

    public StrategyCommandHandler(
        IMediator mediator,
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
            _logger.LogError("<pre>Invalid command format. Use: /strategy [create|delete|stop|resume] [parameters]</pre>");
            return;
        }

        var parts = parameters.Trim().Split([' '], 2);
        var subCommand = parts[0].ToLower();
        var subParameters = parts.Length > 1 ? parts[1] : string.Empty;

        try
        {
            switch (subCommand)
            {
                case "create":
                    await HandleCreate(subParameters);
                    break;
                case "delete":
                    await HandleDelete(subParameters);
                    break;
                case "stop":
                    await HandleStop();
                    break;
                case "resume":
                    await HandleResume();
                    break;
                default:
                    _logger.LogError("<pre>Unknown command. Use: create, delete, stop, or resume</pre>");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "<pre>Strategy command execution failed</pre>");
        }
    }

    private async Task HandleCreate(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentNullException(nameof(json));
        }

        var command = JsonConvert.DeserializeObject<CreateStrategyCommand>(json) ?? throw new InvalidOperationException("<pre>Failed to parse strategy parameters</pre>");
        await _mediator.Send(command);
        _logger.LogInformation("<pre>策略创建成功 ✅</pre>");
    }

    private async Task HandleDelete(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id), "<pre>Strategy ID cannot be null or empty</pre>");
        }

        var command = new DeleteStrategyCommand { Id = id.Trim() };
        var result = await _mediator.Send(command);

        if (!result)
        {
            throw new InvalidOperationException($"<pre>Failed to delete strategy {id}</pre>");
        }

        _logger.LogInformation("<pre>策略[{StrategyId}]已删除 ✅</pre>", id);
    }

    private async Task HandleStop(string id = "")
    {
        var result = await _strategyRepository.UpdateStatusAsync(StateStatus.Paused);
        if (result)
        {
            _logger.LogInformation("<pre>策略已成功暂停 ⏸️</pre>");
        }
    }

    private async Task HandleResume(string id = "")
    {
        var result = await _strategyRepository.UpdateStatusAsync(StateStatus.Running);
        if (result)
        {
            _logger.LogInformation("<pre>策略已成功恢复运行️</pre>");
        }
    }

    public Task HandleCallbackAsync(string callbackData)
    {
        throw new NotImplementedException();
    }
}
