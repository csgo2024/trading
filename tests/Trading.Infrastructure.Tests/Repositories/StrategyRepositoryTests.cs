using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class StrategyRepositoryTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly StrategyRepository _repository;

    public StrategyRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new StrategyRepository(_fixture.MongoContext);
    }

    [Fact]
    public async Task Add_WithUniqueStrategy_ShouldAddSuccessfully()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            AccountType = AccountType.Spot,
            Amount = 100,
            PriceDropPercentage = 0.1m,
            Status = StateStatus.Running
        };

        // Act
        var result = await _repository.Add(strategy);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.Equal(strategy.Symbol, result.Symbol);
    }

    [Fact]
    public async Task Add_WithDuplicateStrategy_ShouldThrowException()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategy = new Strategy
        {
            Symbol = "ETHUSDT",
            AccountType = AccountType.Feature,
            Amount = 100,
            Status = StateStatus.Running
        };
        await _repository.Add(strategy);

        var duplicateStrategy = new Strategy
        {
            Symbol = "ETHUSDT",
            AccountType = AccountType.Feature,
            Amount = 200,
            Status = StateStatus.Running
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.Add(duplicateStrategy));
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task GetAllStrategies_ShouldReturnAllStrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "BTC1", AccountType = AccountType.Spot },
            new() { Symbol = "BTC2", AccountType = AccountType.Feature }
        };

        foreach (var strategy in strategies)
        {
            await _repository.Add(strategy);
        }

        // Act
        var result = await _repository.GetAllStrategies();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Symbol == "BTC1");
        Assert.Contains(result, s => s.Symbol == "BTC2");
    }

    [Fact]
    public async Task InitializeFeatureStrategies_ShouldReturnOnlyRunningFeatureStrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "F1", AccountType = AccountType.Feature, Status = StateStatus.Running },
            new() { Symbol = "F2", AccountType = AccountType.Feature, Status = StateStatus.Paused },
            new() { Symbol = "S1", AccountType = AccountType.Spot, Status = StateStatus.Running }
        };

        foreach (var strategy in strategies)
        {
            await _repository.Add(strategy);
        }

        // Act
        var result = await _repository.InitializeFeatureStrategies();

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey("F1"));
    }

    [Fact]
    public async Task InitializeSpotStrategies_ShouldReturnOnlyRunningSpotStrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "S1", AccountType = AccountType.Spot, Status = StateStatus.Running },
            new() { Symbol = "S2", AccountType = AccountType.Spot, Status = StateStatus.Paused },
            new() { Symbol = "F1", AccountType = AccountType.Feature, Status = StateStatus.Running }
        };

        foreach (var strategy in strategies)
        {
            await _repository.Add(strategy);
        }

        // Act
        var result = await _repository.InitializeSpotStrategies();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(result.ContainsKey("S1"));
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldUpdateStrategySuccessfully()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            AccountType = AccountType.Spot,
            Status = StateStatus.Running
        };
        var addedStrategy = await _repository.Add(strategy);

        // Update status
        addedStrategy.Status = StateStatus.Paused;

        // Act
        var result = await _repository.UpdateOrderStatusAsync(addedStrategy);

        // Assert
        Assert.True(result);
        var updatedStrategy = await _repository.GetByIdAsync(addedStrategy.Id);
        Assert.Equal(StateStatus.Paused, updatedStrategy.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateAllStrategiesStatus()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "S1", Status = StateStatus.Running },
            new() { Symbol = "S2", Status = StateStatus.Running }
        };

        foreach (var strategy in strategies)
        {
            await _repository.Add(strategy);
        }

        // Act
        var result = await _repository.UpdateStatusAsync(StateStatus.Paused);

        // Assert
        Assert.True(result);
        var allStrategies = await _repository.GetAllStrategies();
        Assert.All(allStrategies, s => Assert.Equal(StateStatus.Paused, s.Status));
    }
}