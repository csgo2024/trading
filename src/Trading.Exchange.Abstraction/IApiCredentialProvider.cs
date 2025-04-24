using Trading.Common.Models;

namespace Trading.Exchange.Abstraction;

public interface IApiCredentialProvider
{
    BinanceSettings GetBinanceSettingsV1();
    BinanceSettings GetBinanceSettingsV2();
}
