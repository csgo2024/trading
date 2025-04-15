using Trading.API.Services.Trading.Account;
using Trading.API.Services.Trading.Executors;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

/*
    The  TradingService  class is a background service that runs every 2 minutes.
    It initializes and executes the strategies that are active in the system.
    It uses the  AccountProcessorFactory  and  ExecutorFactory  to
    get the appropriate account processor and executor for each strategy.
    The  ExecuteAsync  method is the main method of the background service.
    It runs in a loop until the service is stopped.
    It calls the  InitializeAndExecuteStrategies  method to initialize and execute the strategies.
    The  InitializeAndExecuteStrategies  method initializes the active strategies
    from the database and then executes them.
    It creates a task for each strategy and executes it using the appropriate account processor and executor.
*/
public class TradingService : BackgroundService
{
    private readonly ILogger<TradingService> _logger;
    private Dictionary<string, Strategy>? _strategies;
    private readonly AccountProcessorFactory _accountProcessorFactory;
    private readonly ExecutorFactory _executorFactory;
    private readonly IStrategyRepository _strategyRepository;

    public TradingService(
        ILogger<TradingService> logger,
        AccountProcessorFactory accountProcessorFactory,
        IStrategyRepository strategyRepository,
        ExecutorFactory executorFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executorFactory = executorFactory ?? throw new ArgumentNullException(nameof(executorFactory));
        _accountProcessorFactory = accountProcessorFactory ?? throw new ArgumentNullException(nameof(accountProcessorFactory));
        _strategyRepository = strategyRepository ?? throw new ArgumentNullException(nameof(strategyRepository));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await InitializeAndExecuteStrategies(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trading strategy execution failed.");
            }
            await SimulateDelay(TimeSpan.FromMinutes(2), cancellationToken);
        }
    }

    private async Task InitializeAndExecuteStrategies(CancellationToken cancellationToken)
    {
        _strategies = await _strategyRepository.InitializeActiveStrategies();
        if (_strategies == null || _strategies.Count == 0)
        {
            return;
        }

        var tasks = _strategies.Values.Select(strategy =>
        {
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            return executor != null && accountProcessor != null
                ? executor.Execute(accountProcessor, strategy, cancellationToken)
                : Task.CompletedTask;
        }).ToList();

        await Task.WhenAll(tasks);
    }
    // 新增的模拟延迟方法
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);  // 默认行为使用 Task.Delay
    }
}
