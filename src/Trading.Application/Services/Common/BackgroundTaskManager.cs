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
    private readonly ConcurrentDictionary<(TaskCategory category, string taskId), (CancellationTokenSource cts, Task task)> _monitoringTasks = new();

    public BackgroundTaskManager(ILogger<BackgroundTaskManager> logger)
    {
        _logger = logger;
        _logger.LogInformation("[BackgroundTaskManager]HashCode: {HashCode}", GetHashCode());
    }

    public Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken)
    {
        var key = (category, taskId);
        _monitoringTasks.GetOrAdd(key, key =>
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var task = Task.Run(() => executionFunc(cts.Token), cts.Token);
            _logger.LogInformation("Task started: Category={Category}, TaskId={TaskId}", category, taskId);
            return (cts, task);
        });
        return Task.CompletedTask;
    }

    public virtual async Task StopAsync(TaskCategory category, string taskId)
    {
        var key = (category, taskId);
        if (_monitoringTasks.TryRemove(key, out var taskInfo))
        {
            try
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
                _logger.LogInformation("Task stopped: Category={Category}, TaskId={TaskId}", category, taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping task: Category={Category}, TaskId={TaskId}", category, taskId);
            }
            finally
            {
                taskInfo.cts.Dispose();
            }

        }
    }

    public virtual async Task StopAsync(TaskCategory category)
    {
        var tasksToRemove = _monitoringTasks.Where(kvp => kvp.Key.category == category).ToList();
        var tasks = tasksToRemove.Select(async task =>
        {
            if (_monitoringTasks.TryRemove(task.Key, out var taskInfo))
            {
                try
                {
                    await taskInfo.cts.CancelAsync();
                    await taskInfo.task;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping task: Category={Category}, TaskId={TaskId}", task.Key.category, task.Key.taskId);
                }
                finally
                {
                    taskInfo.cts.Dispose();
                }
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("All tasks stopped for category: {Category}", category);
    }

    public string[] GetActiveTaskIds(TaskCategory category) =>
        _monitoringTasks.Keys
            .Where(k => k.category == category)
            .Select(k => k.taskId)
            .ToArray();

    public virtual async Task StopAsync()
    {
        var tasks = _monitoringTasks.Select(async kvp =>
        {
            var (_, taskInfo) = kvp;
            try
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping task");
            }
            finally
            {
                taskInfo.cts.Dispose();
            }
        });

        await Task.WhenAll(tasks);
        _monitoringTasks.Clear();

        _logger.LogInformation("All tasks stopped across all categories");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
