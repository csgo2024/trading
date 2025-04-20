using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class AlarmRepository : BaseRepository<Alarm>, IAlarmRepository
{
    public AlarmRepository(IMongoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Alarm>> GetActiveAlarmsAsync(CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.IsActive).ToListAsync(cancellationToken);
    }
    public IEnumerable<Alarm> GetActiveAlarms(string symbol)
    {
        return _collection.Find(x => x.IsActive && x.Symbol == symbol).ToList();
    }

    public IEnumerable<Alarm> GetAlarmsById(string[] ids)
    {
        var filter = Builders<Alarm>.Filter.In(x => x.Id, ids);
        return _collection.Find(filter).ToList();
    }

    public async Task<bool> DeactivateAlarmAsync(string alarmId, CancellationToken cancellationToken)
    {
        var update = Builders<Alarm>.Update.Set(x => x.IsActive, false);
        var result = await _collection.UpdateOneAsync(x => x.Id == alarmId, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> ClearAllAlarmsAsync(CancellationToken cancellationToken)
    {
        var deleteResult = await _collection.DeleteManyAsync(
            Builders<Alarm>.Filter.Empty,
            cancellationToken);

        return (int)deleteResult.DeletedCount;
    }

    public async Task<List<Alarm>> GetAllAlerts()
    {
        var filter = Builders<Alarm>.Filter.Empty;
        var strategies = await _collection.Find(filter).SortBy(x => x.Symbol).SortBy(x => x.CreatedAt).ToListAsync();
        return strategies;
    }
}
