using MongoDB.Driver;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.API.Tests;

public class TestDataInitializer
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<Strategy> _strategies;

    public TestDataInitializer(IMongoDatabase database)
    {
        _database = database;
        _strategies = database.GetCollection<Strategy>("strategy");
    }

    public async Task ResetTestData()
    {
        await CleanTestData();
        await SeedTestStrategies();
    }

    public async Task CleanTestData()
    {
        await _strategies.DeleteManyAsync(Builders<Strategy>.Filter.Empty);
    }

    private async Task SeedTestStrategies()
    {
        var strategies = new List<Strategy>
        {
            new()
            {
                Id = "test-strategy-1",
                Symbol = "BTCUSDT",
                Amount = 100,
                Volatility = 0.1m,
                AccountType = AccountType.Spot,
                StrategyType = StrategyType.BottomBuy,
                Status = Status.Running,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Leverage = 1
            },
            new()
            {
                Id = "test-strategy-2",
                Symbol = "ETHUSDT",
                Amount = 200,
                Volatility = 0.2m,
                AccountType = AccountType.Future,
                StrategyType = StrategyType.BottomBuy,
                Status = Status.Paused,
                CreatedAt = DateTime.UtcNow,
                Leverage = 2
            }
        };

        await _strategies.InsertManyAsync(strategies);
    }
}
