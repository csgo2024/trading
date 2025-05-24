using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class CredentialSettingRepository : BaseRepository<CredentialSetting>, ICredentialSettingRepository
{
    public CredentialSettingRepository(IMongoDbContext context, IDomainEventDispatcher domainEventDispatcher)
        : base(context, domainEventDispatcher)
    {
    }

    public CredentialSetting? GetEncryptedRawSetting()
    {
        var setting = _collection.Find(x => x.Status == 1).FirstOrDefault();
        return setting;
    }
}
