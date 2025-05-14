namespace Trading.Exchange.Abstraction.Contracts;

public class CredentialSettingV1
{
    public string ApiKey { get; set; } = "your-apikey";
    public string ApiSecret { get; set; } = "your-secret";
}
public class CredentialSettingV2
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}
