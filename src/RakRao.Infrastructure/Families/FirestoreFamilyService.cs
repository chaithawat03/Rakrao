using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using RakRao.Application.Families;
using RakRao.Domain.Audit;
using RakRao.Domain.Families;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure.Families;

public sealed class FirestoreFamilyService(FirestoreDb db, IFirestoreUnitOfWork work, TimeProvider clock,
    IConfiguration configuration) : IFamilyService
{
    private static readonly string[] FamilyRoles = ["CREATOR", "FAMILY_ADMIN", "FAMILY_MEMBER"];

    private async Task<Guid?> UserIdAsync(string uid, CancellationToken ct)
    {
        var mapping = await db.Document(FirestoreDocuments.UidPath(uid)).GetSnapshotAsync(ct);
        return mapping.Exists ? mapping.Id("userId") : null;
    }

    // Every authority check, including UID mapping, is re-read inside the transaction on every retry.
    private static async Task<(DocumentSnapshot Family, Guid? UserId, string[] Roles)> AccessAsync(
        FirestoreUnitOfWork.Attempt tx, string uid, Guid familyId)
    {
        var family = await tx.ReadAsync($"families/{familyId}");
        var mapping = await tx.ReadAsync(FirestoreDocuments.UidPath(uid));
        if (!family.Exists || family.Text("status") != "ACTIVE" || !mapping.Exists) return (family, null, []);
        var userId = mapping.Id("userId");
        var member = await tx.ReadAsync(FirestoreDocuments.MembershipPath(familyId, userId));
        if (!member.Exists || member.Text("status") != "ACTIVE") return (family, null, []);
        var roles = new List<string>();
        foreach (var role in FamilyRoles)
        {
            var key = await tx.ReadAsync(FirestoreDocuments.RolePath(familyId, userId, role));
            if (key.Exists) roles.Add(role);
        }
        return (family, userId, roles.ToArray());
    }

    private static FamilySummary Summary(DocumentSnapshot family, string[] roles) => Summary(family.Id(), family.Text("name")!,
        family.Text("description"), family.GetValue<int>("version"), roles);

    private static FamilySummary Summary(Guid id, string name, string? description, int version, string[] roles)
    {
        var capabilities = new List<string> { "READ_FAMILY" };
        if (roles.Contains("FAMILY_ADMIN") || roles.Contains("CREATOR")) capabilities.AddRange(["EDIT_FAMILY", "MANAGE_MEMBER_ROLES"]);
        if (roles.Contains("CREATOR")) capabilities.AddRange(["MANAGE_MEMBERS", "MANAGE_ROLES"]);
        return new(id, name, description, version, roles, capabilities);
    }

    public async Task<FamilyCreateResult> CreateAsync(string firebaseUid, CreateFamilyRequest request,
        string? idempotencyKey, string requestId, CancellationToken cancellationToken)
    {
        // Immutable identity lookup allocates operation entities before retries; authority is still read in the transaction.
        var userId = await UserIdAsync(firebaseUid, cancellationToken);
        if (userId is null) return new(FamilyCreateOutcome.NotProvisioned, null);
        var now = clock.GetUtcNow();
        var keyHash = idempotencyKey is null ? null : FirestoreDocuments.Hash(idempotencyKey);
        var canonical = JsonSerializer.Serialize(new { name = request.Name!.Trim(), description = request.Description?.Trim() });
        var requestHash = idempotencyKey is null ? null : Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(idempotencyKey), Encoding.UTF8.GetBytes(canonical)));
        var safeDiff = keyHash is null ? null : JsonSerializer.Serialize(new { idempotencyKeyHash = keyHash, requestHash });
        var family = new Family(request.Name!.Trim(), request.Description?.Trim(), userId.Value, now);
        var membership = new FamilyMembership(family.Id, userId.Value, now);
        var creator = new RoleAssignment(family.Id, userId.Value, "CREATOR", userId.Value, now);
        var admin = new RoleAssignment(family.Id, userId.Value, "FAMILY_ADMIN", userId.Value, now);
        var audit = new AuditEvent(userId, "FAMILY_CREATED", "FAMILY", family.Id, requestId, now, family.Id, safeDiff: safeDiff);
        var requestPath = keyHash is null ? null : $"family_creation_requests/{FirestoreDocuments.Hash($"{userId}_{keyHash}")}";
        var hourlyLimit = int.TryParse(configuration["FamilyCreation:MaxPerHour"], out var limit) ? Math.Max(1, limit) : 3;
        return await work.RunAsync(async tx =>
        {
            var identity = await tx.ReadAsync(FirestoreDocuments.UidPath(firebaseUid));
            if (!identity.Exists || identity.Id("userId") != userId) return new FamilyCreateResult(FamilyCreateOutcome.NotProvisioned, null);
            if (requestPath is not null)
            {
                var prior = await tx.ReadAsync(requestPath);
                if (prior.Exists)
                {
                    if (prior.Text("requestHash") != requestHash) return new FamilyCreateResult(FamilyCreateOutcome.Conflict, null);
                    var access = await AccessAsync(tx, firebaseUid, prior.Id("familyId"));
                    return access.UserId is not null && access.Roles.Length > 0
                        ? new FamilyCreateResult(FamilyCreateOutcome.Created, Summary(access.Family, access.Roles))
                        : new FamilyCreateResult(FamilyCreateOutcome.Conflict, null);
                }
            }
            var limitPath = $"family_creation_limits/{userId}";
            var state = await tx.ReadAsync(limitPath);
            var cutoff = Timestamp.FromDateTimeOffset(now.AddHours(-1));
            var recent = state.Exists ? state.GetValue<List<Timestamp>>("createdAt").Where(t => t >= cutoff).ToList() : [];
            if (recent.Count >= hourlyLimit) return new FamilyCreateResult(FamilyCreateOutcome.Throttled, null);
            recent.Add(Timestamp.FromDateTimeOffset(now));
            tx.Create($"families/{family.Id}", FirestoreDocuments.Family(family));
            tx.Create(FirestoreDocuments.MembershipPath(family.Id, userId.Value), FirestoreDocuments.Membership(membership));
            foreach (var role in new[] { creator, admin })
            {
                tx.Create($"role_assignments/{role.Id}", FirestoreDocuments.Role(role));
                tx.Create(FirestoreDocuments.RolePath(family.Id, userId.Value, role.Role), FirestoreDocuments.Role(role));
            }
            tx.Create($"family_guards/{family.Id}", new() { ["activeCreatorCount"] = 1, ["version"] = 1 });
            tx.Set(limitPath, new() { ["createdAt"] = recent });
            if (requestPath is not null) tx.Create(requestPath, new() { ["userId"] = userId.ToString(),
                ["familyId"] = family.Id.ToString(), ["keyHash"] = keyHash, ["requestHash"] = requestHash });
            tx.Add(audit);
            return new FamilyCreateResult(FamilyCreateOutcome.Created, Summary(family.Id, family.Name, family.Description, 1, ["CREATOR", "FAMILY_ADMIN"]));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FamilySummary>> ListAsync(string firebaseUid, CancellationToken cancellationToken)
    {
        var userId = await UserIdAsync(firebaseUid, cancellationToken);
        if (userId is null) return [];
        var memberships = await db.Collection("family_memberships").WhereEqualTo("userId", userId.ToString())
            .WhereEqualTo("status", "ACTIVE").GetSnapshotAsync(cancellationToken);
        var result = new List<FamilySummary>();
        foreach (var membership in memberships)
        {
            var family = await GetAsync(firebaseUid, membership.Id("familyId"), cancellationToken);
            if (family is not null) result.Add(family);
        }
        return result.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Id).ToArray();
    }

    public Task<FamilySummary?> GetAsync(string firebaseUid, Guid familyId, CancellationToken cancellationToken) =>
        work.RunAsync<FamilySummary?>(async tx =>
        {
            var access = await AccessAsync(tx, firebaseUid, familyId);
            return access.UserId is null || access.Roles.Length == 0 ? null : Summary(access.Family, access.Roles);
        }, cancellationToken);

    public async Task<FamilyUpdateResult> UpdateAsync(string firebaseUid, Guid familyId, int expectedVersion,
        UpdateFamilyRequest request, string requestId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var auditId = Guid.NewGuid();
        return await work.RunAsync(async tx =>
        {
            var access = await AccessAsync(tx, firebaseUid, familyId);
            if (access.UserId is null) return new FamilyUpdateResult(FamilyUpdateOutcome.NotFound, null);
            if (!access.Roles.Contains("CREATOR") && !access.Roles.Contains("FAMILY_ADMIN")) return new FamilyUpdateResult(FamilyUpdateOutcome.Forbidden, null);
            if (access.Family.GetValue<int>("version") != expectedVersion) return new FamilyUpdateResult(FamilyUpdateOutcome.Conflict, null);
            tx.Update($"families/{familyId}", new() { ["name"] = request.Name!.Trim(), ["description"] = request.Description?.Trim(),
                ["version"] = checked(expectedVersion + 1), ["updatedAt"] = Timestamp.FromDateTimeOffset(now), ["updatedByUserId"] = access.UserId.ToString() });
            CreateAudit(tx, auditId, access.UserId.Value, "FAMILY_UPDATED", "FAMILY", familyId, familyId, requestId, now);
            return new FamilyUpdateResult(FamilyUpdateOutcome.Updated, Summary(familyId, request.Name.Trim(), request.Description?.Trim(), expectedVersion + 1, access.Roles));
        }, cancellationToken);
    }

    public async Task<RoleChangeOutcome> ChangeRoleAsync(string firebaseUid, Guid familyId, Guid memberId,
        ChangeFamilyRoleRequest request, string requestId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var assignmentId = Guid.NewGuid();
        var auditId = Guid.NewGuid();
        return await work.RunAsync(async tx =>
        {
            var access = await AccessAsync(tx, firebaseUid, familyId);
            if (access.UserId is null) return RoleChangeOutcome.NotFound;
            var canManage = request.Role == "FAMILY_MEMBER" ? access.Roles.Contains("CREATOR") || access.Roles.Contains("FAMILY_ADMIN") : access.Roles.Contains("CREATOR");
            if (!canManage) return RoleChangeOutcome.Forbidden;
            var members = await tx.ReadAsync(db.Collection("family_memberships").WhereEqualTo("familyId", familyId.ToString())
                .WhereEqualTo("id", memberId.ToString()).Limit(1));
            if (members.Count == 0 || members[0].Text("status") != "ACTIVE") return RoleChangeOutcome.NotFound;
            var memberUser = members[0].Id("userId");
            if (request.Grant == true && memberUser == access.UserId) return RoleChangeOutcome.Forbidden;
            var rolePath = FirestoreDocuments.RolePath(familyId, memberUser, request.Role!);
            var active = await tx.ReadAsync(rolePath);
            var guardPath = $"family_guards/{familyId}";
            var guard = await tx.ReadAsync(guardPath);
            if (!guard.Exists) throw new InvalidOperationException("Family invariant record is missing.");
            var count = guard.GetValue<int>("activeCreatorCount");
            if (request.Grant != true && !active.Exists) return RoleChangeOutcome.Changed;
            if (request.Grant != true && request.Role == "CREATOR" && count <= 1) return RoleChangeOutcome.Conflict;
            var target = active.Exists ? active.Id() : assignmentId;
            if (request.Grant == true && !active.Exists)
            {
                var data = new Dictionary<string, object?> { ["id"] = target.ToString(), ["familyId"] = familyId.ToString(),
                    ["userId"] = memberUser.ToString(), ["role"] = request.Role, ["grantedByUserId"] = access.UserId.ToString(),
                    ["grantedAt"] = Timestamp.FromDateTimeOffset(now), ["revokedAt"] = null, ["reason"] = null };
                tx.Create($"role_assignments/{target}", data);
                tx.Create(rolePath, data);
                if (request.Role == "CREATOR") count++;
            }
            else if (request.Grant != true)
            {
                tx.Update($"role_assignments/{target}", new() { ["revokedAt"] = Timestamp.FromDateTimeOffset(now), ["reason"] = null });
                tx.Delete(rolePath);
                if (request.Role == "CREATOR") count--;
            }
            // All supported role mutations serialize on this guard, independently of metadata ETags.
            // A future membership-status mutation must adjust this count in the same guarded transaction.
            tx.Update(guardPath, new() { ["activeCreatorCount"] = count, ["version"] = checked(guard.GetValue<int>("version") + 1) });
            CreateAudit(tx, auditId, access.UserId.Value, request.Grant == true ? "FAMILY_ROLE_GRANTED" : "FAMILY_ROLE_REVOKED",
                "ROLE_ASSIGNMENT", target, familyId, requestId, now);
            return RoleChangeOutcome.Changed;
        }, cancellationToken);
    }

    private static void CreateAudit(FirestoreUnitOfWork.Attempt tx, Guid id, Guid actor, string action, string targetType,
        Guid target, Guid familyId, string requestId, DateTimeOffset now) => tx.Create($"audit_events/{id}", new()
        {
            ["id"] = id.ToString(), ["actorUserId"] = actor.ToString(), ["action"] = action, ["targetType"] = targetType,
            ["targetId"] = target.ToString(), ["familyId"] = familyId.ToString(), ["requestId"] = requestId,
            ["occurredAt"] = Timestamp.FromDateTimeOffset(now), ["reason"] = null, ["safeDiff"] = null
        });
}
