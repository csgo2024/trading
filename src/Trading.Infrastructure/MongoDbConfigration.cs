using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Infrastructure;

public static class MongoDbConfigration
{
    private static readonly object _lock = new object();
    private static bool _initialized;

    public static void Configure()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            RegisterClassMap<BaseEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(p => p.Id);
                cm.MapIdMember(p => p.Id)
                    .SetIdGenerator(StringObjectIdGenerator.Instance);
            });

            RegisterClassMap<CredentialSettings>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });

            RegisterClassMap<Strategy>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });

            _initialized = true;
        }
    }

    private static void RegisterClassMap<T>(Action<BsonClassMap<T>> mapper) where T : class
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            BsonClassMap.RegisterClassMap(mapper);
        }
    }
}