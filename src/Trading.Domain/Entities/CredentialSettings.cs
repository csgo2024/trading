namespace Trading.Domain.Entities;

public class CredentialSettings: BaseEntity
{
    public byte[] ApiKey { get; set; }
    public byte[] ApiSecret { get; set; }
    
    public int Status { get; set; }
}