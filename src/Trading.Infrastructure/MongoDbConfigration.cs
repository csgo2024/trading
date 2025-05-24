using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Conventions;

namespace Trading.Infrastructure;

public static class MongoDbConfigration
{
    private static readonly object _lock = new object();
    private static bool _initialized;

    public static void Configure()
    {
        if (_initialized)
        {
            return;
        }

        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            var pack = new ConventionPack
            {
                new SnakeCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true)
            };

            ConventionRegistry.Register("CustomConventions", pack, t => true);

            RegisterClassMap<BaseEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(p => p.Id);
                cm.MapIdMember(p => p.Id)
                    .SetIdGenerator(StringObjectIdGenerator.Instance);
            });

            RegisterClassMap<Strategy>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });

            RegisterClassMap<Alert>(cm =>
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
