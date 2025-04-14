using Trading.API.Services.Alerts;

namespace Trading.API.HostServices;

public class PriceAlertService : BackgroundService
{
    private readonly ILogger<PriceAlertService> _logger;
    private readonly PriceAlertManager _alertManager;

    public PriceAlertService(
        ILogger<PriceAlertService> logger,
        PriceAlertManager alertManager)
    {
        _logger = logger;
        _alertManager = alertManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _alertManager.LoadPriceAlertsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Price alert service error");
            }
            await SimulateDelay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _alertManager.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    // 新增的模拟延迟方法
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken stoppingToken)
    {
        return Task.Delay(delay, stoppingToken);  // 默认行为使用 Task.Delay
    }
}
