using Trading.API.Services.Trading.Account;
using Trading.API.Services.Trading.Executors;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _strategies = await _strategyRepository.InitializeActiveStrategies();
                if (_strategies != null && _strategies.Count > 0)
                {
                    var tasks = new List<Task>();
                    foreach (var strategy in _strategies.Values)
                    {
                        var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
                        var executor = _executorFactory.GetExecutor(strategy.StrategyType);
                        if (executor != null && accountProcessor != null)
                        {
                            tasks.Add(executor.Execute(accountProcessor, strategy, stoppingToken));
                        }
                    }
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trading strategy execution failed.");
            }
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}