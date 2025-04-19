using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Trading.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class StrategyCommandHandler : ICommandHandler
{
    private readonly ILogger<StrategyCommandHandler> _logger;
    private readonly IMediator _mediator;
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
                    await HandleStop(subParameters);
                    break;
                case "resume":
                    await HandleResume(subParameters);
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
    }

    private async Task HandleStop(string id)
    {
        _ = await _strategyRepository.UpdateStatusAsync(StateStatus.Paused);
        await _mediator.Publish(new StrategyPausedEvent(id.Trim()));
    }

    private async Task HandleResume(string id)
    {
        _ = await _strategyRepository.UpdateStatusAsync(StateStatus.Running);
        var strategy = await _strategyRepository.GetByIdAsync(id.Trim());
        await _mediator.Publish(new StrategyResumedEvent(strategy));
    }

    public Task HandleCallbackAsync(string callbackData)
    {
        throw new NotImplementedException();
    }
}
