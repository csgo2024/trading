using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Trading.Common.Enums;

namespace Trading.Application.Services.Common;

public interface IBackgroundTaskManager : IAsyncDisposable
{
    Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken);
    Task StopAsync(TaskCategory category, string taskId);
    Task StopAsync(TaskCategory category);
    Task StopAsync();
    string[] GetActiveTaskIds(TaskCategory category);
}

public class BackgroundTaskManager : IBackgroundTaskManager
{
    private readonly ILogger<BackgroundTaskManager> _logger;
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private static readonly ConcurrentDictionary<(TaskCategory category, string taskId), (CancellationTokenSource cts, Task task)> _monitoringTasks = new();

    public BackgroundTaskManager(ILogger<BackgroundTaskManager> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken)
    {
        var key = (category, taskId);
        await _taskLock.WaitAsync(cancellationToken);
        try
        {
            if (_monitoringTasks.ContainsKey(key))
            {
                return;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var task = Task.Run(() => executionFunc(cts.Token), cts.Token);
            _monitoringTasks.TryAdd(key, (cts, task));
            _logger.LogInformation("Task started: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting stopping task: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public async Task StopAsync(TaskCategory category, string taskId)
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

    public async Task StopAsync(TaskCategory category)
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

    public string[] GetActiveTaskIds(TaskCategory category) =>
        _monitoringTasks.Keys
            .Where(k => k.category == category)
            .Select(k => k.taskId)
            .ToArray();

    public async Task StopAsync()
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

