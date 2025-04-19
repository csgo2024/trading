using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Common;

namespace Trading.Application.Tests.Services.Common;

public class BackgroundTaskManagerTests : IAsyncDisposable
{
    private readonly Mock<ILogger<BackgroundTaskManager>> _loggerMock;
    private readonly BackgroundTaskManager _taskManager;
    private readonly CancellationTokenSource _cts;

    public BackgroundTaskManagerTests()
    {
        _loggerMock = new Mock<ILogger<BackgroundTaskManager>>();
        _taskManager = new BackgroundTaskManager(_loggerMock.Object);
        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task StartAsync_ShouldAddTaskToMonitoring()
    {
        // Arrange
        var taskId = "test-task";
        var executed = false;

        // Act
        await _taskManager.StartAsync(
            TaskCategories.Strategy,
            taskId,
            async ct =>
            {
                executed = true;
                await Task.Delay(100, ct);
            },
            _cts.Token);

        await Task.Delay(200); // Wait for task execution

        // Assert
        Assert.True(executed);
        Assert.Contains(taskId, _taskManager.GetActiveTaskIds(TaskCategories.Strategy));
        await _taskManager.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTaskAlreadyExists_ShouldNotStartNewTask()
    {
        // Arrange
        var taskId = "test-task";
        var executionCount = 0;

        // Act
        await _taskManager.StartAsync(
            TaskCategories.Strategy,
            taskId,
            async ct =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(100, ct);
            },
            _cts.Token);

        await _taskManager.StartAsync(
            TaskCategories.Strategy,
            taskId,
            async ct =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(100, ct);
            },
            _cts.Token);

        await Task.Delay(200);

        // Assert
        Assert.Equal(1, executionCount);
        await _taskManager.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldRemoveAndCancelTask()
    {
        // Arrange
        var taskId = "test-task";
        var cancellationRequested = false;

        await _taskManager.StartAsync(
            TaskCategories.Strategy,
            taskId,
            async ct =>
            {
                try
                {
                    await Task.Delay(5000, ct);
                }
                catch (OperationCanceledException)
                {
                    cancellationRequested = true;
                    throw;
                }
            },
            _cts.Token);

        // Act
        await _taskManager.StopAsync(TaskCategories.Strategy, taskId);

        // Assert
        Assert.True(cancellationRequested);
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategories.Strategy));
        await _taskManager.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WithCategory_ShouldStopAllTasksInCategory()
    {
        // Arrange
        var taskIds = new[] { "task1", "task2" };
        var executingTasks = 0;

        foreach (var taskId in taskIds)
        {
            await _taskManager.StartAsync(
                TaskCategories.Strategy,
                taskId,
                async ct =>
                {
                    Interlocked.Increment(ref executingTasks);
                    try
                    {
                        await Task.Delay(100, ct);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref executingTasks);
                    }
                },
                _cts.Token);
        }

        await Task.Delay(2000); // Wait for tasks to start

        // Act
        await _taskManager.StopAsync(TaskCategories.Strategy);

        // Assert
        Assert.Equal(0, executingTasks);
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategories.Strategy));
    }

    [Fact]
    public async Task GetActiveTaskIds_ShouldReturnCorrectTasksForCategory()
    {
        // Arrange
        var strategyTaskId = "strategy-task";
        var alarmTaskId = "alarm-task";

        await _taskManager.StartAsync(
            TaskCategories.Strategy,
            strategyTaskId,
            ct => Task.Delay(1000, ct),
            _cts.Token);

        await _taskManager.StartAsync(
            TaskCategories.Alarm,
            alarmTaskId,
            ct => Task.Delay(1000, ct),
            _cts.Token);

        // Act
        var strategyTasks = _taskManager.GetActiveTaskIds(TaskCategories.Strategy);
        var alarmTasks = _taskManager.GetActiveTaskIds(TaskCategories.Alarm);

        // Assert
        Assert.Single(strategyTasks);
        Assert.Equal(strategyTaskId, strategyTasks[0]);
        Assert.Single(alarmTasks);
        Assert.Equal(alarmTaskId, alarmTasks[0]);
    }

    [Fact]
    public async Task StopAsync_ShouldStopAllTasks()
    {
        // Arrange
        var executingTasks = 0;
        var tasks = new[]
        {
            (TaskCategories.Strategy, "strategy-task"),
            (TaskCategories.Alarm, "alarm-task")
        };

        foreach (var (category, taskId) in tasks)
        {
            await _taskManager.StartAsync(
                category,
                taskId,
                async ct =>
                {
                    Interlocked.Increment(ref executingTasks);
                    try
                    {
                        await Task.Delay(1000, ct);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref executingTasks);
                    }
                },
                _cts.Token);
        }

        await Task.Delay(2000); // Wait for tasks to start

        // Act
        await _taskManager.StopAsync();

        // Assert
        Assert.Equal(0, executingTasks);
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategories.Strategy));
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategories.Alarm));
    }

    public async ValueTask DisposeAsync()
    {
        await _taskManager.DisposeAsync();
        _cts.Dispose();
    }
}
