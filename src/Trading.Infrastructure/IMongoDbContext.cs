using MongoDB.Driver;

namespace Trading.Infrastructure;

public interface IMongoDbContext
{

    IMongoCollection<T> GetCollection<T>() where T : class;

    Task<bool> Ping();
}
