using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading;

public class StrategyDispatchService :
    INotificationHandler<StrategyCreatedEvent>,
    INotificationHandler<StrategyDeletedEvent>,
    INotificationHandler<StrategyPausedEvent>,
    INotificationHandler<StrategyResumedEvent>
{
    private readonly IAccountProcessorFactory _accountProcessorFactory;
    private readonly IExecutorFactory _executorFactory;
    private readonly IBackgroundTaskManager _backgroundTaskManager;

    public StrategyDispatchService(ILogger<StrategyDispatchService> logger,
                                    IAccountProcessorFactory accountProcessorFactory,
                                    IExecutorFactory executorFactory,
                                    IBackgroundTaskManager backgroundTaskManager,
                                    IStrategyRepository strategyRepository)
    {
        _accountProcessorFactory = accountProcessorFactory;
        _executorFactory = executorFactory;
        _backgroundTaskManager = backgroundTaskManager;
    }

    public async Task Handle(StrategyCreatedEvent notification, CancellationToken cancellationToken)
    {
        await DispatchAsync(cancellationToken);
    }

    public async Task Handle(StrategyDeletedEvent notification, CancellationToken cancellationToken)
    {
        var strategy = notification.Strategy;
        var executor = _executorFactory.GetExecutor(strategy.StrategyType);
        if (strategy.OrderId.HasValue)
        {
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            await executor!.CancelExistingOrder(accountProcessor!, strategy, cancellationToken);
        }
        executor!.RemoveFromMonitoringStrategy(strategy);
        await _backgroundTaskManager.StopAsync(TaskCategory.Strategy, strategy.Id);
    }

    public async Task Handle(StrategyPausedEvent notification, CancellationToken cancellationToken)
    {
        var strategy = notification.Strategy;
        var executor = _executorFactory.GetExecutor(strategy.StrategyType);
        executor!.RemoveFromMonitoringStrategy(strategy);
        await _backgroundTaskManager.StopAsync(TaskCategory.Strategy, strategy.Id);
    }

    public async Task Handle(StrategyResumedEvent notification, CancellationToken cancellationToken)
    {
        await DispatchAsync(cancellationToken);
    }

    public virtual async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var strategyTypes = Enum.GetValues(typeof(StrategyType)).Cast<StrategyType>();
        var tasks = strategyTypes.Select(async strategyType =>
        {
            var executor = _executorFactory.GetExecutor(strategyType);
            await executor!.LoadActiveStratey(cancellationToken);
            foreach (var strategy in executor!.GetMonitoringStrategy().Values)
            {
                var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType)!;
                await _backgroundTaskManager.StartAsync(TaskCategory.Strategy,
                                                        strategy.Id,
                                                        async (ct) => await executor.ExecuteLoopAsync(accountProcessor, strategy, ct),
                                                        cancellationToken);
            }
        });
        await Task.WhenAll(tasks);
    }
}
