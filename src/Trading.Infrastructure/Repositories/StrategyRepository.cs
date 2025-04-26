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

        return await AddAsync(entity, cancellationToken);
    }

    public async Task<Dictionary<string, Strategy>> InitializeActiveStrategies()
    {
        var data = await _collection.Find(x => x.Status == StateStatus.Running).ToListAsync();
        return data.ToDictionary(config => $"{config.Symbol}{config.Id}{config.AccountType}", config => config);
    }

    public async Task<List<Strategy>> GetAllStrategies()
    {
        var filter = Builders<Strategy>.Filter.Empty;
        var strategies = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.AccountType).ToListAsync();
        return strategies;
    }

    public async Task<Dictionary<string, Strategy>?> InitializeFutureStrategies()
    {
        var data = await _collection.Find(x => x.AccountType == AccountType.Future && x.Status == StateStatus.Running).ToListAsync();
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

    public async Task<List<Strategy>> Find(string symbol,
                                           string interval,
                                            StrategyType strategyType,
                                           CancellationToken cancellationToken = default)
    {
        var data = await _collection.Find(x => x.Symbol == symbol
                                               && x.Status == StateStatus.Running
                                               && x.StrategyType == strategyType
                                               && x.Interval == interval).ToListAsync(cancellationToken: cancellationToken);
        return data;
    }
}
