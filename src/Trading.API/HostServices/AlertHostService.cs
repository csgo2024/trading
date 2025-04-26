using Trading.Application.Services.Alerts;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class AlertHostService : BackgroundService
{
    private readonly AlertNotificationService _sendAlertService;
    private readonly IAlertRepository _alertRepository;
    private readonly IStrategyRepository _strategyRepository;

    private readonly ILogger<AlertHostService> _logger;
    private readonly IKlineStreamManager _klineStreamManager;

    public AlertHostService(ILogger<AlertHostService> logger,
                            IKlineStreamManager klineStreamManager,
                            AlertNotificationService sendAlertService,
                            IStrategyRepository strategyRepository,
                            IAlertRepository alertRepository)
    {
        _logger = logger;
        _klineStreamManager = klineStreamManager;
        _sendAlertService = sendAlertService;
        _alertRepository = alertRepository;
        _strategyRepository = strategyRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        bool isSubscribed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var alerts = await _alertRepository.GetActiveAlertsAsync(cancellationToken);
                var strategyDict = await _strategyRepository.InitializeActiveStrategies();

                var symbols = alerts.Select(x => x.Symbol)
                    .Concat(strategyDict.Values.Select(x => x.Symbol))
                    .ToHashSet();

                var intervals = alerts.Select(x => x.Interval)
                    .Concat(strategyDict.Values.Where(x => x.Interval != null)
                    .Select(x => x.Interval!))
                    .ToHashSet();

                await _sendAlertService.InitWithAlerts(alerts, cancellationToken);
                var needReconnect = _klineStreamManager.NeedsReconnection();

                if (!isSubscribed && symbols.Count > 0)
                {
                    isSubscribed = await _klineStreamManager.SubscribeSymbols(symbols, intervals, cancellationToken);
                    if (isSubscribed)
                    {
                        _logger.LogInformation("Initial subscription completed successfully");
                    }
                }
                if (needReconnect && symbols.Count > 0)
                {
                    isSubscribed = await _klineStreamManager.SubscribeSymbols(symbols, intervals, cancellationToken);
                    if (isSubscribed)
                    {
                        _logger.LogInformation("Reconnection completed successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = !isSubscribed
                    ? "Initial subscription failed. Retrying in 1 minute..."
                    : "Reconnection failed. Retrying in 1 minute...";

                _logger.LogError(ex, errorMessage);
            }

            await SimulateDelay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }

    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
