using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Trading.Common.Helpers;
using Trading.Domain.IRepositories;
using Trading.Exchange.Abstraction;
using Trading.Exchange.Abstraction.Contracts;

namespace Trading.Infrastructure;

public class ApiCredentialProvider : IApiCredentialProvider
{
    private readonly IOptions<CredentialSettingV2> _credentialSetting;

    private readonly IConfiguration _configuration;

    private readonly ICredentialSettingRepository _credentialSettingRepository;

    public ApiCredentialProvider(IOptions<CredentialSettingV2> credentialSetting,
                                 IConfiguration configuration,
                                 ICredentialSettingRepository credentialSettingRepository)
    {
        _credentialSettingRepository = credentialSettingRepository;
        _credentialSetting = credentialSetting;
        _configuration = configuration;
    }

    public CredentialSettingV1 GetCredentialSettingsV1()
    {
        var result = new CredentialSettingV1();
        var privateKey = _configuration.GetSection("PrivateKey")?.Value ?? string.Empty;
        var settings = _credentialSettingRepository.GetEncryptedRawSetting();
        if (settings == null)
        {
            return result;
        }

        var (apiKey, apiSecret) = RsaEncryptionHelper.DecryptApiCredentialFromBytes(settings.ApiKey,
                                                                                    settings.ApiSecret,
                                                                                    privateKey);

        result.ApiKey = apiKey;
        result.ApiSecret = apiSecret;
        return result;
    }
    public CredentialSettingV1 GetCredentialSettingsV2()
    {
        var (apiKey, apiSecret) = RsaEncryptionHelper.DecryptApiCredentialFromBase64(_credentialSetting.Value.ApiKey,
                                                                                     _credentialSetting.Value.ApiSecret,
                                                                                     _credentialSetting.Value.PrivateKey);
        return new CredentialSettingV1
        {
            ApiKey = apiKey,
            ApiSecret = apiSecret
        };
    }

}
