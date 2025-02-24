namespace Trading.Domain.Entities;

public class CredentialSettings : BaseEntity
{
    public byte[] ApiKey { get; set; } = Array.Empty<byte>();
    public byte[] ApiSecret { get; set; } = Array.Empty<byte>();
    public int Status { get; set; }
}
