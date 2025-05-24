using Trading.Application.Services.Trading;

namespace Trading.API.HostServices;

public class TradingHostService : BackgroundService
{
    private readonly ILogger<TradingHostService> _logger;
    private readonly StrategyDispatchService _strategyDispatchService;

    public TradingHostService(ILogger<TradingHostService> logger,
                              StrategyDispatchService strategyDispatchService)
    {
        _logger = logger;
        _strategyDispatchService = strategyDispatchService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _strategyDispatchService.DispatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing trading service");
            }
            await SimulateDelay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
