using MongoDB.Driver;
using Testcontainers.MongoDb;
using Trading.Domain;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class MongoDbFixture : IAsyncLifetime
{
    public MongoDbContainer MongoDbContainer { get; }
    public IMongoDbContext MongoContext { get; private set; }
    
    public MongoDbFixture()
    {
        MongoDbContainer = new MongoDbBuilder()
            .WithName($"test-mongo-{Guid.NewGuid()}")
            .WithPortBinding(27017, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await MongoDbContainer.StartAsync();
        
        var mongoClient = new MongoClient(MongoDbContainer.GetConnectionString());
        MongoContext = new MongoDbContext(mongoClient.GetDatabase(Guid.NewGuid().ToString()));
        MongoDbConfigration.Configure();
    }

    public async Task DisposeAsync()
    {
        await MongoDbContainer.DisposeAsync();
    }
}