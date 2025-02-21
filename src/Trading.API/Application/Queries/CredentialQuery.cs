using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Application.Queries
{
    public class CredentialQuery : ICredentialQuery
    {
        private readonly ICredentialSettingRepository _credentialSettingRepository;
        public CredentialQuery(ICredentialSettingRepository credentialSettingRepository)
        { 
            _credentialSettingRepository = credentialSettingRepository;
        }

        public CredentialSettings? GetCredential()
        {
            return _credentialSettingRepository.GetCredentialSetting();
        }
    }
}
