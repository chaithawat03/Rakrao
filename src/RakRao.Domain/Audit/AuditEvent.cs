namespace RakRao.Domain.Audit;

public sealed class AuditEvent
{
    private AuditEvent() { }

    public AuditEvent(Guid? actorUserId, string action, string targetType, Guid targetId,
        string requestId, DateTimeOffset occurredAt, Guid? familyId = null,
        string? reason = null, string? safeDiff = null)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        RequestId = requestId;
        OccurredAt = occurredAt;
        FamilyId = familyId;
        Reason = reason;
        SafeDiff = safeDiff;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public Guid? FamilyId { get; private set; }
    public string RequestId { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? SafeDiff { get; private set; }
}
