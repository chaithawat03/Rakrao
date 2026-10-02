namespace RakRao.Domain.Identity;

public sealed class User
{
    private User() { }

    public User(string firebaseUid, string? displayName, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FirebaseUid = firebaseUid;
        DisplayName = displayName;
        CreatedAt = now;
        UpdatedAt = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string FirebaseUid { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public int Version { get; private set; }
}
