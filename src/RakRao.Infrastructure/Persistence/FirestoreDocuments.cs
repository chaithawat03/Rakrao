using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using RakRao.Domain.Audit;
using RakRao.Domain.Families;
using RakRao.Domain.Identity;

namespace RakRao.Infrastructure.Persistence;

// Explicit mapping keeps Firestore types and storage field names out of Domain/Application.
public static class FirestoreDocuments
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string UidPath(string uid) => $"firebase_uid_mappings/{Hash(uid)}";
    public static string MembershipPath(Guid familyId, Guid userId) => $"family_memberships/{familyId}_{userId}";
    public static string RolePath(Guid familyId, Guid userId, string role) => $"active_role_keys/{familyId}_{userId}_{role}";
    public static string? Text(this DocumentSnapshot snapshot, string field) => snapshot.TryGetValue<string>(field, out var value) ? value : null;
    public static Guid Id(this DocumentSnapshot snapshot, string field = "id") => Guid.Parse(snapshot.GetValue<string>(field));
    public static Dictionary<string, object?> User(User user) => new()
    {
        ["id"] = user.Id.ToString(), ["firebaseUid"] = user.FirebaseUid, ["displayName"] = user.DisplayName,
        ["createdAt"] = Timestamp.FromDateTimeOffset(user.CreatedAt), ["updatedAt"] = Timestamp.FromDateTimeOffset(user.UpdatedAt),
        ["createdByUserId"] = user.CreatedByUserId?.ToString(), ["updatedByUserId"] = user.UpdatedByUserId?.ToString(), ["version"] = user.Version
    };
    public static Dictionary<string, object?> Family(Family family) => new()
    {
        ["id"] = family.Id.ToString(), ["name"] = family.Name, ["description"] = family.Description, ["status"] = family.Status,
        ["createdAt"] = Timestamp.FromDateTimeOffset(family.CreatedAt), ["updatedAt"] = Timestamp.FromDateTimeOffset(family.UpdatedAt),
        ["createdByUserId"] = family.CreatedByUserId.ToString(), ["updatedByUserId"] = family.UpdatedByUserId?.ToString(), ["version"] = family.Version
    };
    public static Dictionary<string, object?> Membership(FamilyMembership member) => new()
    {
        ["id"] = member.Id.ToString(), ["familyId"] = member.FamilyId.ToString(), ["userId"] = member.UserId.ToString(), ["status"] = member.Status,
        ["joinedAt"] = Timestamp.FromDateTimeOffset(member.JoinedAt), ["leftAt"] = null,
        ["createdAt"] = Timestamp.FromDateTimeOffset(member.CreatedAt), ["updatedAt"] = Timestamp.FromDateTimeOffset(member.UpdatedAt),
        ["createdByUserId"] = member.CreatedByUserId?.ToString(), ["updatedByUserId"] = member.UpdatedByUserId?.ToString(), ["version"] = member.Version
    };
    public static Dictionary<string, object?> Role(RoleAssignment role) => new()
    {
        ["id"] = role.Id.ToString(), ["familyId"] = role.FamilyId?.ToString(), ["userId"] = role.UserId.ToString(), ["role"] = role.Role,
        ["grantedByUserId"] = role.GrantedByUserId.ToString(), ["grantedAt"] = Timestamp.FromDateTimeOffset(role.GrantedAt),
        ["revokedAt"] = role.RevokedAt is { } revoked ? Timestamp.FromDateTimeOffset(revoked) : null, ["reason"] = role.Reason
    };
    public static Dictionary<string, object?> Audit(AuditEvent audit) => new()
    {
        ["id"] = audit.Id.ToString(), ["occurredAt"] = Timestamp.FromDateTimeOffset(audit.OccurredAt),
        ["actorUserId"] = audit.ActorUserId?.ToString(), ["action"] = audit.Action, ["targetType"] = audit.TargetType,
        ["targetId"] = audit.TargetId.ToString(), ["familyId"] = audit.FamilyId?.ToString(), ["requestId"] = audit.RequestId,
        ["reason"] = audit.Reason, ["safeDiff"] = audit.SafeDiff
    };
}
