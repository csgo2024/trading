using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Trading.Application.Services.Alarms;

public class AlarmTaskManager : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, (CancellationTokenSource cts, Task task)> _monitoringTasks = new();
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private readonly ILogger<AlarmTaskManager> _logger;

    public AlarmTaskManager(ILogger<AlarmTaskManager> logger)
    {
        _logger = logger;
    }

    public virtual Task StartMonitor(string alarmId, Func<CancellationToken, Task> monitoringFunc, CancellationToken cancellationToken)
    {
        if (_monitoringTasks.ContainsKey(alarmId))
        {
            return Task.CompletedTask;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = Task.Run(() => monitoringFunc(cts.Token), cancellationToken);
        _monitoringTasks.TryAdd(alarmId, (cts, task));
        return Task.CompletedTask;
    }

    public virtual async Task StopMonitor(string alarmId)
    {
        await _taskLock.WaitAsync();
        try
        {
            if (_monitoringTasks.TryRemove(alarmId, out var taskInfo))
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
                taskInfo.cts.Dispose();
                _logger.LogInformation("Alarm task for {AlarmId} removed successfully.", alarmId);
            }
            else
            {
                _logger.LogError("Alarm task {AlarmId} not found.", alarmId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping monitoring for alarm {AlarmId}", alarmId);
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public virtual async Task StopAllMonitor()
    {
        await _taskLock.WaitAsync();
        try
        {
            foreach (var (_, taskInfo) in _monitoringTasks)
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
                taskInfo.cts.Dispose();
            }
            _monitoringTasks.Clear();
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllMonitor();
        _taskLock.Dispose();
    }

    public string[] GetMonitoringAlarmIds() => _monitoringTasks.Keys.ToArray();
}
