using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Alarms;

namespace Trading.Application.Tests.Services.Alarms;

public class AlarmTaskManagerTests
{
    private readonly Mock<ILogger<AlarmTaskManager>> _loggerMock;
    private readonly AlarmTaskManager _taskManager;
    private readonly CancellationTokenSource _cts;

    public AlarmTaskManagerTests()
    {
        _loggerMock = new Mock<ILogger<AlarmTaskManager>>();
        _taskManager = new AlarmTaskManager(_loggerMock.Object);
        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task StartMonitor_WhenAlarmIdAlreadyExists_ShouldNotStartNewTask()
    {
        // Arrange
        var alarmId = "test-alarm-1";
        var callCount = 0;

        Task MonitoringFunc(CancellationToken ct)
        {
            callCount++;
            return Task.CompletedTask;
        }

        // Act
        await _taskManager.Start(alarmId, MonitoringFunc, _cts.Token);
        Thread.Sleep(1000);
        await _taskManager.Start(alarmId, MonitoringFunc, _cts.Token);

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task StopMonitor_WhenAlarmExists_ShouldStopAndRemoveTask()
    {
        // Arrange
        var alarmId = "test-alarm-2";
        var taskCompleted = false;

        async Task MonitoringFunc(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                taskCompleted = true;
            }
        }

        // Act
        await _taskManager.Start(alarmId, MonitoringFunc, _cts.Token);
        await _taskManager.Stop(alarmId);

        // Assert
        Assert.True(taskCompleted);
    }

    [Fact]
    public async Task StopAllMonitor_ShouldStopAllTasks()
    {
        // Arrange
        var completedTasks = 0;
        var totalTasks = 3;

        async Task MonitoringFunc(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref completedTasks);
            }
        }

        // Act
        for (var i = 0; i < totalTasks; i++)
        {
            await _taskManager.Start($"test-alarm-{i}", MonitoringFunc, _cts.Token);
        }
        await _taskManager.Stop();

        // Assert
        Assert.Equal(totalTasks, completedTasks);
    }

    [Fact]
    public async Task DisposeAsync_ShouldStopAllTasksAndDispose()
    {
        // Arrange
        var taskCompleted = false;

        async Task MonitoringFunc(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                taskCompleted = true;
            }
        }

        // Act
        await _taskManager.Start("test-alarm", MonitoringFunc, _cts.Token);
        await _taskManager.DisposeAsync();

        // Assert
        Assert.True(taskCompleted);
    }
}
