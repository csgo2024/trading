using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface ICredentialSettingRepository : IRepository<CredentialSetting>
{
    CredentialSetting? GetEncryptedRawSetting();
}
