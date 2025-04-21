using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class AlertRepository : BaseRepository<Alert>, IAlertRepository
{
    public AlertRepository(IMongoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.Status == StateStatus.Running).ToListAsync(cancellationToken);
    }
    public IEnumerable<Alert> GetActiveAlerts(string symbol)
    {
        return _collection.Find(x => x.Status == StateStatus.Running && x.Symbol == symbol).ToList();
    }

    public IEnumerable<Alert> GetAlertsById(string[] ids)
    {
        var filter = Builders<Alert>.Filter.In(x => x.Id, ids);
        return _collection.Find(filter).ToList();
    }

    public async Task<bool> DeactivateAlertAsync(string alertId, CancellationToken cancellationToken)
    {
        var update = Builders<Alert>.Update.Set(x => x.Status, StateStatus.Paused);
        var result = await _collection.UpdateOneAsync(x => x.Id == alertId, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> ClearAllAlertsAsync(CancellationToken cancellationToken)
    {
        var deleteResult = await _collection.DeleteManyAsync(
            Builders<Alert>.Filter.Empty,
            cancellationToken);

        return (int)deleteResult.DeletedCount;
    }

    public async Task<List<Alert>> GetAllAlerts()
    {
        var filter = Builders<Alert>.Filter.Empty;
        var strategies = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.CreatedAt).ToListAsync();
        return strategies;
    }
}
