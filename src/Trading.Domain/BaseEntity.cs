namespace Trading.Domain;

public abstract class BaseEntity : IEntity
{
    public string Id { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
