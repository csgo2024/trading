using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Trading.Application.Services.Trading;

public class StrategyTaskManager : IAsyncDisposable
{
    private readonly ILogger<StrategyTaskManager> _logger;
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private static readonly ConcurrentDictionary<string, (CancellationTokenSource cts, Task task)> _monitoringTasks = new();

    public StrategyTaskManager(ILogger<StrategyTaskManager> logger)
    {
        _logger = logger;
    }

    public virtual Task Start(string key, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken)
    {
        if (_monitoringTasks.ContainsKey(key))
        {
            return Task.CompletedTask;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = Task.Run(() => executionFunc(cts.Token), cancellationToken);
        _monitoringTasks.TryAdd(key, (cts, task));
        return Task.CompletedTask;
    }

    public virtual async Task Stop(string key)
    {
        await _taskLock.WaitAsync();
        try
        {
            if (_monitoringTasks.TryRemove(key, out var taskInfo))
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
                taskInfo.cts.Dispose();
                _logger.LogInformation("Task for {Key} removed successfully.", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping task for {Key}", key);
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public virtual async Task StopAll()
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
        await StopAll();
        _taskLock.Dispose();
    }

    public string[] GetExecutingKey() => _monitoringTasks.Keys.ToArray();
}
