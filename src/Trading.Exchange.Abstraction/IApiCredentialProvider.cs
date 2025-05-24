using Trading.Exchange.Abstraction.Contracts;

namespace Trading.Exchange.Abstraction;

public interface IApiCredentialProvider
{
    CredentialSettingV1 GetCredentialSettingsV1();
    CredentialSettingV1 GetCredentialSettingsV2();
}
