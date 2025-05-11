using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Trading.Common.Models;
using Trading.Domain.IRepositories;
using Trading.Exchange.Abstraction;
using Trading.Infrastructure.Repositories;

namespace Trading.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDbSettings"));

        services.AddSingleton(provider =>
        {
            var value = provider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var settings = MongoClientSettings.FromConnectionString(value.ConnectionString);
            var client = new MongoClient(settings);
            return client.GetDatabase(value.DatabaseName);
        });

        services.AddSingleton<IMongoDbContext, MongoDbContext>();
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

        services.AddSingleton<IStrategyRepository, StrategyRepository>();
        services.AddSingleton<IAlertRepository, AlertRepository>();
        services.AddSingleton<ICredentialSettingRepository, CredentialSettingRepository>();
        services.AddSingleton<IApiCredentialProvider, ApiCredentialProvider>();
        services.AddTransient<IDomainEventDispatcher, DomainEventDispatcher>();
        MongoDbConfigration.Configure();
        return services;
    }
}
