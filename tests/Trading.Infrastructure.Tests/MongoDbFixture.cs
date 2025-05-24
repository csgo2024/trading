using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;
using Xunit;

namespace Trading.Infrastructure.Tests;

public class MongoDbFixture : IAsyncLifetime
{
    public MongoDbContainer MongoDbContainer { get; }
    public IMongoDbContext? MongoContext { get; private set; }
    public IDomainEventDispatcher DomainEventDispatcher { get; }

    public MongoDbFixture()
    {
        MongoDbContainer = new MongoDbBuilder()
            .WithName($"Trading-Infrastructure-Tests-{Guid.NewGuid()}")
            .WithPortBinding(27017, true)
            .Build();
        DomainEventDispatcher = new Mock<IDomainEventDispatcher>().Object;
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
