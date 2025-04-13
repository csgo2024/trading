using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class PriceAlertRepository : BaseRepository<PriceAlert>, IPriceAlertRepository
{
    public PriceAlertRepository(IMongoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<PriceAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.IsActive).ToListAsync(cancellationToken);
    }

    public async Task<bool> DeactivateAlertAsync(string alertId, CancellationToken cancellationToken)
    {
        var update = Builders<PriceAlert>.Update.Set(x => x.IsActive, false);
        var result = await _collection.UpdateOneAsync(x => x.Id == alertId, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }
}
