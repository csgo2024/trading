using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class StrategyRepositoryTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly StrategyRepository _repository;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public StrategyRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _domainEventDispatcher = fixture.DomainEventDispatcher;
        _repository = new StrategyRepository(_fixture.MongoContext!, _domainEventDispatcher);
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
            Volatility = 0.1m,
            Status = Status.Running
        };

        // Act
        var result = await _repository.Add(strategy);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.Equal(strategy.Symbol, result.Symbol);
    }

    [Fact]
    public async Task Add_WithDuplicateStrategy_ShouldBeAddedSuccessfully()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategy = new Strategy
        {
            Symbol = "ETHUSDT",
            AccountType = AccountType.Future,
            Amount = 100,
            Status = Status.Running
        };
        await _repository.Add(strategy);

        var duplicateStrategy = new Strategy
        {
            Symbol = "ETHUSDT",
            AccountType = AccountType.Future,
            Amount = 200,
            Status = Status.Running
        };

        await _repository.Add(duplicateStrategy);

        // Act & Assert
        var result = await _repository.GetAllStrategies();
        Assert.Equal(2, result.Count);

    }

    [Fact]
    public async Task GetAllStrategies_ShouldReturnAllStrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "BTC1", AccountType = AccountType.Spot },
            new() { Symbol = "BTC2", AccountType = AccountType.Future }
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
    public async Task FindActiveStrategies_ShouldReturnAllActivestrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "S1", AccountType = AccountType.Spot, Status = Status.Running },
            new() { Symbol = "S2", AccountType = AccountType.Spot, Status = Status.Paused },
            new() { Symbol = "F1", AccountType = AccountType.Future, Status = Status.Running }
        };

        foreach (var strategy in strategies)
        {
            await _repository.Add(strategy);
        }

        // Act
        var result = await _repository.FindActiveStrategies();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Keys.Count);
        Assert.Single(result.Keys, x => x.Contains("S1") && x.Contains(AccountType.Spot.ToString()));
        Assert.Single(result.Keys, x => x.Contains("F1") && x.Contains(AccountType.Future.ToString()));
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
            Status = Status.Running
        };
        var addedStrategy = await _repository.Add(strategy);

        Assert.NotNull(addedStrategy);
        // Update status
        addedStrategy.Status = Status.Paused;

        // Act
        var result = await _repository.UpdateOrderStatusAsync(addedStrategy);

        // Assert
        Assert.True(result);
        var updatedStrategy = await _repository.GetByIdAsync(addedStrategy.Id);
        Assert.NotNull(updatedStrategy);
        Assert.Equal(Status.Paused, updatedStrategy.Status);
    }
    [Fact]
    public async Task FindActiveStrategyByType_ShouldReturnRunningAndExactMatchedStrategies()
    {
        // Arrange
        await _repository.EmptyAsync();
        var strategies = new List<Strategy>
        {
            new() { Symbol = "S1", Interval = "5m", StrategyType = StrategyType.TopSell, Status = Status.Running },
            new() { Symbol = "S1", Interval = "5m", StrategyType = StrategyType.TopSell, Status = Status.Running },
            new() { Symbol = "S1", Interval = "15m", StrategyType = StrategyType.TopSell, Status = Status.Running },
            new() { Symbol = "S1", Interval = "5m", StrategyType = StrategyType.TopSell, Status = Status.Paused },
            new() { Symbol = "S1", Interval = "5m", StrategyType = StrategyType.BottomBuy, Status = Status.Running },
        };

        foreach (var strategy in strategies)
        {
            await _repository.Add(strategy);
        }

        // Act
        var result = await _repository.FindActiveStrategyByType(StrategyType.TopSell, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal("S1", s.Symbol));
        Assert.All(result, s => Assert.Equal(StrategyType.TopSell, s.StrategyType));
        Assert.All(result, s => Assert.Equal(Status.Running, s.Status));
    }
}
