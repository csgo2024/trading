using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Trading.Common.Extensions;
using Trading.Common.Models;
using Trading.Domain.IRepositories;
using Trading.Infrastructure.Repositories;

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
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(
            configuration.GetSection("MongoDbSettings"));

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
        services.AddSingleton<ICredentialSettingRepository, CredentialSettingRepository>();
        services.AddSingleton<IAlertRepository, AlertRepository>();
        MongoDbConfigration.Configure();
        return services;
    }
}
