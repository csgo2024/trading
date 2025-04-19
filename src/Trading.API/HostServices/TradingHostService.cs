using Trading.Application.Services.Trading;

namespace Trading.API.HostServices;

public class TradingHostService : BackgroundService
{
    private readonly ILogger<TradingHostService> _logger;
    private readonly StrategyExecutionService _strategyExecutionService;

    public TradingHostService(
        ILogger<TradingHostService> logger,
        StrategyExecutionService strategyExecutionService)
    {
        _logger = logger;
        _strategyExecutionService = strategyExecutionService;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _strategyExecutionService.ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing trading service");
            }
            await SimulateDelay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
