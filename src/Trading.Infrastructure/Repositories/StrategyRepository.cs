using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class StrategyRepository : BaseRepository<Strategy>, IStrategyRepository
{
    public StrategyRepository(IMongoDbContext context) : base(context)
    {
    }

    public async Task<Strategy?> Add(Strategy entity, CancellationToken cancellationToken = default)
    {
        var exist = await _collection.Find(x => x.Symbol == entity.Symbol && x.AccountType == entity.AccountType).FirstOrDefaultAsync(cancellationToken);
        if (exist != null)
        {
            throw new InvalidOperationException($"[{entity.AccountType}-{entity.Symbol}] already exists.");
        }
        return await AddAsync(entity, cancellationToken);
    }

    public async Task<Dictionary<string, Strategy>> InitializeActiveStrategies()
    {
        var data = await _collection.Find(x => x.Status == StateStatus.Running).ToListAsync();
        return data.ToDictionary(config => $"{config.Symbol}{config.AccountType}", config => config);
    }

    public async Task<List<Strategy>> GetAllStrategies()
    {
        var filter = Builders<Strategy>.Filter.Empty;
        var strategies = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.AccountType).ToListAsync();
        return strategies;
    }

    public async Task<Dictionary<string, Strategy>?> InitializeFeatureStrategies()
    {
        var data = await _collection.Find(x => x.AccountType == AccountType.Feature && x.Status == StateStatus.Running).ToListAsync();
        return data.ToDictionary(config => config.Symbol, config => config);
    }

    public async Task<Dictionary<string, Strategy>?> InitializeSpotStrategies()
    {
        var data = await _collection.Find(x => x.AccountType == AccountType.Spot && x.Status == StateStatus.Running).ToListAsync();
        return data.ToDictionary(config => config.Symbol, config => config);
    }

    public async Task<bool> UpdateOrderStatusAsync(Strategy entity, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(entity.Id, entity, cancellationToken);
    }
    public async Task<bool> UpdateStatusAsync(StateStatus newStatus)
    {
        var filter = Builders<Strategy>.Filter.Empty; // 匹配所有文档
        var update = Builders<Strategy>.Update
            .Set(d => d.Status, newStatus)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        _ = await _collection.UpdateManyAsync(filter, update);
        return true;
    }
}
