namespace RakRao.Domain.Families;

public sealed class FamilyMembership
{
    private FamilyMembership() { }

    public FamilyMembership(Guid familyId, Guid userId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FamilyId = familyId;
        UserId = userId;
        Status = "ACTIVE";
        JoinedAt = now;
        CreatedAt = now;
        UpdatedAt = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public int Version { get; private set; }
}
