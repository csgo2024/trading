using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Trading.API.HostServices;

namespace Trading.API.Tests
{
    public class TradingApiFixture : WebApplicationFactory<Program>, IDisposable
    {
        public IMongoDatabase Database { get; private set; }
        private MongoClient _client;

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            return new WebHostBuilder()
                .UseStartup<Startup>(); // 使用 Program 类作为启动类
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(config => {
                config.AddInMemoryCollection(
                [
                    new KeyValuePair<string, string?>("MongoDbSettings:ConnectionString", "mongodb://localhost:27017"),
                    new KeyValuePair<string, string?>("MongoDbSettings:DatabaseName", "InMemoryDbForTesting")
                ]);
            });
            builder.ConfigureServices(services =>
            {
                // // Remove the app's MongoDB registration.
                // var descriptor = services.SingleOrDefault(
                //     d => d.ServiceType ==
                //         typeof(IMongoDatabase));

                // if (descriptor != null)
                // {
                //     services.Remove(descriptor);
                // }

                // // Add a MongoDB context using an in-memory database for testing.
                // services.AddSingleton<IMongoDatabase>(sp =>
                // {
                //     var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
                //     _client = new MongoClient(settings);
                //     return _client.GetDatabase("InMemoryDbForTesting");
                // });

                // Remove the BackgroundService registration.
                var hostedServiceDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IHostedService) &&
                         d.ImplementationType == typeof(SpotTradingService));

                if (hostedServiceDescriptor != null)
                {
                    services.Remove(hostedServiceDescriptor);
                }

                // Build the service provider.
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database context (IMongoDatabase).
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    Database = scopedServices.GetRequiredService<IMongoDatabase>();
                }
            });
        }

        public new void Dispose()
        {
            if (_client != null)
            {
                // Clean up the database after tests
                _client.DropDatabase("InMemoryDbForTesting");
            }
            base.Dispose();
        }
    }
}