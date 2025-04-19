using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading;

public class StrategyExecutionService :
    INotificationHandler<StrategyCreatedEvent>,
    INotificationHandler<StrategyDeletedEvent>,
    INotificationHandler<StrategyPausedEvent>,
    INotificationHandler<StrategyResumedEvent>
{
    private readonly AccountProcessorFactory _accountProcessorFactory;
    private readonly ExecutorFactory _executorFactory;
    private readonly ILogger<StrategyExecutionService> _logger;
    private readonly IStrategyRepository _strategyRepository;
    private readonly StrategyTaskManager _strategyTaskManager;

    public StrategyExecutionService(
        ILogger<StrategyExecutionService> logger,
        AccountProcessorFactory accountProcessorFactory,
        ExecutorFactory executorFactory,
        StrategyTaskManager strategyTaskManager,
        IStrategyRepository strategyRepository)
    {
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _executorFactory = executorFactory;
        _strategyTaskManager = strategyTaskManager;
        _strategyRepository = strategyRepository;
    }

    public async Task Handle(StrategyCreatedEvent notification, CancellationToken cancellationToken)
    {
        var strategy = notification.Strategy;
        await StartStrategyExecution(strategy, cancellationToken);
    }

    public async Task Handle(StrategyDeletedEvent notification, CancellationToken cancellationToken)
    {
        await _strategyTaskManager.Stop(notification.Id);
    }

    public async Task Handle(StrategyPausedEvent notification, CancellationToken cancellationToken)
    {
        await _strategyTaskManager.Stop(notification.Id);
    }

    public async Task Handle(StrategyResumedEvent notification, CancellationToken cancellationToken)
    {
        await StartStrategyExecution(notification.Strategy, cancellationToken);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var strategies = await _strategyRepository.InitializeActiveStrategies();
            foreach (var strategy in strategies.Values)
            {
                await StartStrategyExecution(strategy, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize strategies");
        }
    }

    private async Task StartStrategyExecution(Strategy strategy, CancellationToken cancellationToken)
    {
        var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
        var executor = _executorFactory.GetExecutor(strategy.StrategyType);

        if (executor == null || accountProcessor == null)
        {
            _logger.LogError("Failed to get executor or account processor for strategy {StrategyId}", strategy.Id);
            return;
        }

        await _strategyTaskManager.Start(
            strategy.Id,
            async (ct) => await ExecuteStrategyLoop(executor, accountProcessor, strategy, ct),
            cancellationToken);
    }

    private async Task ExecuteStrategyLoop(IExecutor executor,
                                           IAccountProcessor accountProcessor,
                                           Strategy strategy,
                                           CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await executor.Execute(accountProcessor, strategy, cancellationToken);
                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing strategy {StrategyId}", strategy.Id);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }
}
