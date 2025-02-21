using MongoDB.Driver;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class CredentialSettingRepository: BaseRepository<CredentialSettings>, ICredentialSettingRepository
{
    public CredentialSettingRepository(IMongoDbContext context) : base(context)
    {
    }

    public  CredentialSettings? GetCredentialSetting()
    {
        var data =  _collection.Find(x =>  x.Status == 1 ).FirstOrDefault();
        return data;
    }
}