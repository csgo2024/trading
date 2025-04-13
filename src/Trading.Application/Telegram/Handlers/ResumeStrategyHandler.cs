using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class ResumeStrategyHandler : ICommandHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<ResumeStrategyHandler> _logger;

    private readonly IStrategyRepository _strategyRepository;

    public static string Command => "/resume";

    public ResumeStrategyHandler(IMediator mediator, ILogger<ResumeStrategyHandler> logger, IStrategyRepository strategyRepository)
    {
        _mediator = mediator;
        _logger = logger;
        _strategyRepository = strategyRepository;
    }

    public async Task HandleAsync(string parameters)
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
