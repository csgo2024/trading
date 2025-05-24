namespace Trading.Domain.Entities;

public class CredentialSetting : BaseEntity
{
    public byte[] ApiKey { get; set; } = Array.Empty<byte>();
    public byte[] ApiSecret { get; set; } = Array.Empty<byte>();
    public int Status { get; set; }
}
