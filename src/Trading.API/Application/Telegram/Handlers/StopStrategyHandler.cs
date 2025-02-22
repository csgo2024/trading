using MediatR;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Application.Telegram.Handlers;

public class StopStrategyHandler : ICommandHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<StopStrategyHandler> _logger;
    private readonly IStrategyRepository _strategyRepository;

    public static string Command => "/stop";

    public StopStrategyHandler(IMediator mediator, ILogger<StopStrategyHandler> logger, IStrategyRepository strategyRepository) 
    {
        _mediator = mediator;
        _logger = logger;
        _strategyRepository = strategyRepository;
    }

    public async Task HandleAsync(string parameters)
    {
        var result = await _strategyRepository.UpdateStatusAsync(StateStatus.Paused);
        if (result)
        {
            _logger.LogInformation("<pre>策略已成功暂停 ⏸️</pre>");
        }
    }
}