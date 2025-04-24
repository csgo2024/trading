using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Trading.Common.Models;
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

    public BinanceSettings GetBinanceSettingsV1()
    {
        var privateKey = _configuration.GetSection("PrivateKey")?.Value ?? string.Empty;
        var settings = _credentialSettingRepository.GetEncryptedRawSetting();
        var apiKey = "your-api-key";
        if (settings?.ApiKey != null)
        {
            apiKey = Encoding.UTF8.GetString(RsaEncryptionHelper.DecryptDataV1(settings.ApiKey, privateKey));
        }
        var apiSecret = "your-secret";
        if (settings?.ApiSecret != null)
        {
            apiSecret = Encoding.UTF8.GetString(RsaEncryptionHelper.DecryptDataV1(settings.ApiSecret, privateKey));
        }
        return new BinanceSettings
        {
            ApiKey = apiKey,
            ApiSecret = apiSecret
        };
    }
    public BinanceSettings GetBinanceSettingsV2()
    {
        var (apiKey, apiSecret) = RsaEncryptionHelper.DecryptApiCredential(_credentialSetting.Value.ApiKey,
                                                                           _credentialSetting.Value.ApiSecret,
                                                                           _credentialSetting.Value.PrivateKey);
        return new BinanceSettings
        {
            ApiKey = apiKey,
            ApiSecret = apiSecret
        };
    }

}
