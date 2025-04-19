using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class AlarmRepositoryTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly AlarmRepository _repository;

    public AlarmRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new AlarmRepository(_fixture.MongoContext!);
    }
    [Fact]
    public async Task GetActiveAlarmsAsync_ShouldReturnOnlyActiveAlarms()
    {
        // Arrange
        var activeAlarm = new Alarm { Id = "1", Symbol = "BTCUSDT", IsActive = true };
        var inactiveAlarm = new Alarm { Id = "2", Symbol = "ETHUSDT", IsActive = false };
        await _repository.AddAsync(activeAlarm);
        await _repository.AddAsync(inactiveAlarm);

        // Act
        var result = await _repository.GetActiveAlarmsAsync(CancellationToken.None);

        // Assert
        var alarms = result.ToList();
        Assert.Single(alarms);
        Assert.Equal(activeAlarm.Id, alarms[0].Id);
    }

    [Fact]
    public async Task GetActiveAlarms_WithSymbol_ShouldReturnMatchingAlarms()
    {
        await _repository.EmptyAsync();
        // Arrange
        var symbol = "BTCUSDT";
        var matchingAlarm = new Alarm { Id = "1", Symbol = symbol, IsActive = true };
        var differentSymbolAlarm = new Alarm { Id = "2", Symbol = "ETHUSDT", IsActive = true };
        await _repository.AddAsync(matchingAlarm);
        await _repository.AddAsync(differentSymbolAlarm);

        // Act
        var result = _repository.GetActiveAlarms(symbol);

        // Assert
        var alarms = result.ToList();
        Assert.Single(alarms);
        Assert.Equal(matchingAlarm.Id, alarms[0].Id);
    }

    [Fact]
    public async Task GetAlarmsById_ShouldReturnMatchingAlarms()
    {
        await _repository.EmptyAsync();
        // Arrange
        var alarm1 = new Alarm { Id = "1", Symbol = "BTCUSDT" };
        var alarm2 = new Alarm { Id = "2", Symbol = "ETHUSDT" };
        var alarm3 = new Alarm { Id = "3", Symbol = "DOGEUSDT" };
        await Task.WhenAll(
            _repository.AddAsync(alarm1),
            _repository.AddAsync(alarm2),
            _repository.AddAsync(alarm3)
        );

        // Act
        var result = _repository.GetAlarmsById(new[] { "1", "3" });

        // Assert
        var alarms = result.ToList();
        Assert.Equal(2, alarms.Count);
        Assert.Contains(alarms, a => a.Id == "1");
        Assert.Contains(alarms, a => a.Id == "3");
    }

    [Fact]
    public async Task DeactivateAlarmAsync_ShouldSetIsActiveToFalse()
    {
        await _repository.EmptyAsync();
        // Arrange
        var alarm = new Alarm { Symbol = "BTCUSDT", IsActive = true };
        await _repository.AddAsync(alarm);

        // Act
        var result = await _repository.DeactivateAlarmAsync(alarm.Id, CancellationToken.None);

        // Assert
        Assert.True(result);
        var deactivatedAlarm = await _repository.GetByIdAsync(alarm.Id);
        Assert.False(deactivatedAlarm.IsActive);
    }

    [Fact]
    public async Task DeactivateAlarmAsync_WithNonExistentId_ShouldReturnFalse()
    {
        await _repository.EmptyAsync();
        // Act
        var result = await _repository.DeactivateAlarmAsync("non-existent", CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ClearAllAlarmsAsync_ShouldRemoveAllAlarms()
    {
        await _repository.EmptyAsync();
        // Arrange
        var alarms = new[]
        {
            new Alarm {  Symbol = "BTCUSDT" },
            new Alarm {  Symbol = "ETHUSDT" }
        };
        await Task.WhenAll(alarms.Select(a => _repository.AddAsync(a)));

        // Act
        var deletedCount = await _repository.ClearAllAlarmsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(alarms.Length, deletedCount);
    }

    [Fact]
    public async Task ClearAllAlarmsAsync_WhenEmpty_ShouldReturnZero()
    {
        await _repository.EmptyAsync();
        // Act
        var deletedCount = await _repository.ClearAllAlarmsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, deletedCount);
    }
}
