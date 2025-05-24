using MongoDB.Driver;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class StrategyRepository : BaseRepository<Strategy>, IStrategyRepository
{
    public StrategyRepository(IMongoDbContext context, IDomainEventDispatcher domainEventDispatcher)
        : base(context, domainEventDispatcher)
    {
    }

    public async Task<Strategy?> Add(Strategy entity, CancellationToken cancellationToken = default)
    {
        var exist = await _collection.Find(x => x.Symbol == entity.Symbol && x.AccountType == entity.AccountType).FirstOrDefaultAsync(cancellationToken);

        return await AddAsync(entity, cancellationToken);
    }

    public async Task<Dictionary<string, Strategy>> FindActiveStrategies()
    {
        var data = await _collection.Find(x => x.Status == Status.Running).ToListAsync();
        return data.ToDictionary(config => $"{config.Symbol}{config.Id}{config.AccountType}", config => config);
    }

    public async Task<List<Strategy>> GetAllStrategies()
    {
        var filter = Builders<Strategy>.Filter.Empty;
        var strategies = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.AccountType).ToListAsync();
        return strategies;
    }
    public async Task<bool> UpdateOrderStatusAsync(Strategy entity, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(entity.Id, entity, cancellationToken);
    }

    public async Task<List<Strategy>> FindActiveStrategyByType(StrategyType strategyType, CancellationToken cancellationToken = default)
    {
        var result = await _collection.Find(x => x.StrategyType == strategyType && x.Status == Status.Running).ToListAsync(cancellationToken);
        return result;
    }
}
