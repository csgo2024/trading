using MongoDB.Bson.Serialization.IdGenerators;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Infrastructure;

using MongoDB.Bson.Serialization;

public static class MongoDbConfigration
{

    public static void Configure()
    {
        BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(p => p.Id);
            cm.MapIdMember(p => p.Id)
                .SetIdGenerator(StringObjectIdGenerator.Instance);
        });
        BsonClassMap.RegisterClassMap<CredentialSettings>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });
        BsonClassMap.RegisterClassMap<Strategy>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });
    }
}