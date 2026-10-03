namespace RakRao.Domain.Families;

public sealed class RoleAssignment
{
    private RoleAssignment() { }

    public RoleAssignment(Guid familyId, Guid userId, string role, Guid grantedByUserId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FamilyId = familyId;
        UserId = userId;
        Role = role;
        GrantedByUserId = grantedByUserId;
        GrantedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? FamilyId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? Reason { get; private set; }

    public void Revoke(DateTimeOffset now, string? reason)
    {
        if (RevokedAt is not null) throw new InvalidOperationException("Role already revoked.");
        RevokedAt = now;
        Reason = reason;
    }
}
