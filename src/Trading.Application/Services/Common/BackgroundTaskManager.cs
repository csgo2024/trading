using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Trading.Application.Services.Common;

public static class TaskCategories
{
    public const string Alert = "Alert";
    public const string Strategy = "Strategy";
}

public interface IBackgroundTaskManager : IAsyncDisposable
{
    Task StartAsync(string category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken);
    Task StopAsync(string category, string taskId);
    Task StopAsync(string category);
    Task StopAsync();
    string[] GetActiveTaskIds(string category);
}

public class BackgroundTaskManager : IBackgroundTaskManager
{
    private readonly ILogger<BackgroundTaskManager> _logger;
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private static readonly ConcurrentDictionary<(string category, string taskId), (CancellationTokenSource cts, Task task)> _monitoringTasks = new();

    public BackgroundTaskManager(ILogger<BackgroundTaskManager> logger)
    {
        _logger = logger;
    }

    public virtual Task StartAsync(string category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken)
    {
        var key = (category, taskId);
        if (_monitoringTasks.ContainsKey(key))
        {
            return Task.CompletedTask;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = Task.Run(() => executionFunc(cts.Token), cancellationToken);
        _monitoringTasks.TryAdd(key, (cts, task));
        _logger.LogInformation("Task started: Category={Category}, TaskId={TaskId}", category, taskId);
        return Task.CompletedTask;
    }

    public virtual async Task StopAsync(string category, string taskId)
    {
        await _taskLock.WaitAsync();
        try
        {
            var key = (category, taskId);
            if (_monitoringTasks.TryRemove(key, out var taskInfo))
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
                taskInfo.cts.Dispose();
                _logger.LogInformation("Task stopped: Category={Category}, TaskId={TaskId}", category, taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping task: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public virtual async Task StopAsync(string category)
    {
        await _taskLock.WaitAsync();
        try
        {
            var tasksToRemove = _monitoringTasks.Where(kvp => kvp.Key.category == category).ToList();
            foreach (var task in tasksToRemove)
            {
                if (_monitoringTasks.TryRemove(task.Key, out var taskInfo))
                {
                    await taskInfo.cts.CancelAsync();
                    await taskInfo.task;
                    taskInfo.cts.Dispose();
                }
            }
            _logger.LogInformation("All tasks stopped for category: {Category}", category);
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public string[] GetActiveTaskIds(string category) =>
        _monitoringTasks.Keys
            .Where(k => k.category == category)
            .Select(k => k.taskId)
            .ToArray();

    public virtual async Task StopAsync()
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
            _logger.LogInformation("All tasks stopped across all categories");
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _taskLock.Dispose();
    }
}
