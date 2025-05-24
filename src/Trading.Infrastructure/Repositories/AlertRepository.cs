using MongoDB.Driver;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class AlertRepository : BaseRepository<Alert>, IAlertRepository
{
    public AlertRepository(IMongoDbContext context, IDomainEventDispatcher domainEventDispatcher)
        : base(context, domainEventDispatcher)
    {
    }

    public async Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.Status == Status.Running).ToListAsync(cancellationToken);
    }
    public IEnumerable<Alert> GetActiveAlerts(string symbol)
    {
        return _collection.Find(x => x.Status == Status.Running && x.Symbol == symbol).ToList();
    }

    public IEnumerable<Alert> GetAlertsById(string[] ids)
    {
        var filter = Builders<Alert>.Filter.In(x => x.Id, ids);
        return _collection.Find(filter).ToList();
    }

    public async Task<bool> DeactivateAlertAsync(string alertId, CancellationToken cancellationToken)
    {
        var update = Builders<Alert>.Update.Set(x => x.Status, Status.Paused);
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

    public async Task<List<string>> ResumeAlertAsync(string symbol, string Interval, CancellationToken cancellationToken)
    {
        var idsToUpdate = await _collection
            .Find(x => x.Symbol == symbol && x.Interval == Interval && x.Status == Status.Paused)
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);

        var update = Builders<Alert>.Update.Set(x => x.Status, Status.Running);
        var result = await _collection.UpdateManyAsync(
            x => x.Symbol == symbol && x.Interval == Interval && x.Status == Status.Paused,
            update,
            cancellationToken: cancellationToken);

        return idsToUpdate;
    }
}
