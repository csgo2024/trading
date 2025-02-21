namespace Trading.Domain.Entities;

public class CredentialSettings: BaseEntity
{
    public string ApiKey { get; set; }
    public string ApiSecret { get; set; }
    
    public int Status { get; set; }
}