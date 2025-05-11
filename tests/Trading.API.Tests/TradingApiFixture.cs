using System.Text;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Trading.API.HostServices;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading;

namespace Trading.API.Tests;

public class TradingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public IMongoDatabase? Database { get; private set; }
    public TestDataInitializer? TestDataInitializer { get; private set; }
    private MongoClient? _client;

    private readonly MongoDbContainer _mongoDbContainer;

    public TradingApiFixture()
    {
        _mongoDbContainer = new MongoDbBuilder()
            .WithName("Trading-API-Tests" + Guid.NewGuid())
            .WithPortBinding(27017, true)
            .Build();
    }

    protected override IWebHostBuilder CreateWebHostBuilder()
    {
        return new WebHostBuilder()
            .UseStartup<Startup>();
    }

    public async Task InitializeAsync()
    {
        await _mongoDbContainer.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("MongoDbSettings:ConnectionString", _mongoDbContainer.GetConnectionString()),
                new KeyValuePair<string, string?>("MongoDbSettings:DatabaseName", "InMemoryDbForTesting"),
                // BotToken Format: {chatId}:{string} , chatId type is long.
                new KeyValuePair<string, string?>("TelegramSettings:BotToken", "6061388873:your-bot-token"),

                new KeyValuePair<string, string?>("CredentialSettings:ApiKey", Convert.ToBase64String(Encoding.UTF8.GetBytes("your-api-secret"))),
                new KeyValuePair<string, string?>("CredentialSettings:ApiSecret", Convert.ToBase64String(Encoding.UTF8.GetBytes("your-api-secret"))),
            ]);
        });

        builder.ConfigureServices(services =>
        {
            // Remove the app's MongoDB registration.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IMongoDatabase));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // // Add a MongoDB context using an in-memory database for testing.
            services.AddSingleton(sp =>
            {
                var settings = MongoClientSettings.FromConnectionString(_mongoDbContainer.GetConnectionString());
                _client = new MongoClient(settings);
                return _client.GetDatabase("InMemoryDbForTesting");
            });

            var hostedServices = new[]
            {
                typeof(TradingHostService),
                typeof(AlertHostService)
            };
            // Remove all hosted services that are not needed for testing.
            foreach (var hostedService in hostedServices)
            {
                var hostedServiceDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IHostedService) &&
                         d.ImplementationType == hostedService);

                if (hostedServiceDescriptor != null)
                {
                    services.Remove(hostedServiceDescriptor);
                }
            }

            // Remove StrategyDispatchService from MediatR notification handlers
            descriptor = services.FirstOrDefault(d =>
                d.ServiceType.IsGenericType &&
                d.ServiceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
                d.ImplementationType == typeof(StrategyDispatchService));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            // Remove AlertNotificationService from MediatR notification handlers
            descriptor = services.FirstOrDefault(d =>
                d.ServiceType.IsGenericType &&
                d.ServiceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
                d.ImplementationType == typeof(AlertNotificationService));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Build the service provider.
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context (IMongoDatabase).
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                Database = scopedServices.GetRequiredService<IMongoDatabase>();
                TestDataInitializer = new TestDataInitializer(Database);
            }
        });
    }

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var taskManager = scope.ServiceProvider.GetService<IBackgroundTaskManager>();
            if (taskManager != null)
            {
                await taskManager.StopAsync();
            }
        }
        // Clean up the database after tests
        _client?.DropDatabase("InMemoryDbForTesting");
        await _mongoDbContainer.DisposeAsync();
    }

    public new void Dispose()
    {
        base.Dispose();
    }
}
