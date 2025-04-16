using Trading.API.Services.Alarms;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class AlarmHostService : BackgroundService
{
    private readonly ILogger<AlarmHostService> _logger;
    private readonly IAlarmRepository _alarmRepository;
    private readonly KlineStreamManager _klineStreamManager;

    private readonly AlarmNotificationService _sendAlarmService;

    public AlarmHostService(
        ILogger<AlarmHostService> logger,
        KlineStreamManager klineStreamManager,
        AlarmNotificationService sendAlarmService,
        IAlarmRepository alarmRepository)
    {
        _logger = logger;
        _klineStreamManager = klineStreamManager;
        _sendAlarmService = sendAlarmService;
        _alarmRepository = alarmRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        bool isSubscribed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var alarms = await _alarmRepository.GetActiveAlarmsAsync(cancellationToken);
                var symbols = alarms.Select(x => x.Symbol).ToHashSet();
                await _sendAlarmService.InitWithAlarms(alarms, cancellationToken);
                var needReconnect = _klineStreamManager.NeedsReconnection();

                if (!isSubscribed && symbols.Count > 0)
                {
                    isSubscribed = await _klineStreamManager.SubscribeSymbols(symbols, cancellationToken);
                    if (isSubscribed)
                    {
                        _logger.LogInformation("Initial subscription completed successfully");
                    }
                }
                if (needReconnect && symbols.Count > 0)
                {
                    isSubscribed = await _klineStreamManager.SubscribeSymbols(symbols, cancellationToken);
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
    }

    // 新增的模拟延迟方法
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);  // 默认行为使用 Task.Delay
    }
}
