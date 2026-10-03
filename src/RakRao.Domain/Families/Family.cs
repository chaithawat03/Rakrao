namespace RakRao.Domain.Families;

public sealed class Family
{
    private Family() { }

    public Family(string name, string? description, Guid creatorUserId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Status = "ACTIVE";
        CreatedByUserId = creatorUserId;
        UpdatedByUserId = creatorUserId;
        CreatedAt = now;
        UpdatedAt = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; }

    public void UpdateDetails(string name, string? description, Guid actorUserId, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
        Version++;
    }
}
