using MongoDB.Bson;
using MongoDB.Driver;
using Trading.Common.Extensions;

namespace Trading.Infrastructure;

public class MongoDbContext : IMongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
    }

    public IMongoCollection<T> GetCollection<T>() where T : class
    {
        return _database.GetCollection<T>(typeof(T).Name.ToSnakeCase());
    }
    public async Task<bool> Ping()
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
