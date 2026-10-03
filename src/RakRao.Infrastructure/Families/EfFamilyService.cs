using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RakRao.Application.Audit;
using RakRao.Application.Families;
using RakRao.Domain.Audit;
using RakRao.Domain.Families;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure.Families;

public sealed class EfFamilyService(RakRaoDbContext db, IAuditWriter auditWriter, TimeProvider clock,
    IConfiguration configuration) : IFamilyService
{
    public async Task<FamilyCreateResult> CreateAsync(string firebaseUid, CreateFamilyRequest request,
        string? idempotencyKey,
        string requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE firebase_uid = {firebaseUid} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return new FamilyCreateResult(FamilyCreateOutcome.NotProvisioned, null);
        var now = clock.GetUtcNow();
        var keyHash = idempotencyKey is null ? null :
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)));
        var canonicalRequest = JsonSerializer.Serialize(new
        {
            name = request.Name!.Trim(), description = request.Description?.Trim()
        });
        var requestHash = idempotencyKey is null ? null : Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(idempotencyKey), Encoding.UTF8.GetBytes(canonicalRequest)));
        var safeDiff = keyHash is null ? null : JsonSerializer.Serialize(new
        {
            idempotencyKeyHash = keyHash, requestHash
        });
        if (safeDiff is not null)
        {
            var previous = await db.AuditEvents.FromSqlInterpolated($"""
                    SELECT * FROM audit_events
                    WHERE actor_user_id = {user.Id} AND action = 'FAMILY_CREATED'
                      AND safe_diff->>'idempotencyKeyHash' = {keyHash}
                    """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (previous is not null)
            {
                using var stored = JsonDocument.Parse(previous.SafeDiff!);
                if (!stored.RootElement.TryGetProperty("requestHash", out var originalRequestHash) ||
                    originalRequestHash.GetString() != requestHash)
                    return new FamilyCreateResult(FamilyCreateOutcome.Conflict, null);
                var visible = await GetAsync(firebaseUid, previous.TargetId, cancellationToken);
                return new FamilyCreateResult(visible is null ? FamilyCreateOutcome.Conflict : FamilyCreateOutcome.Created,
                    visible);
            }
        }
        var hourlyLimit = int.TryParse(configuration["FamilyCreation:MaxPerHour"], out var configuredLimit)
            ? Math.Max(1, configuredLimit) : 3;
        var recentCreations = await db.AuditEvents.CountAsync(a => a.ActorUserId == user.Id &&
            a.Action == "FAMILY_CREATED" && a.OccurredAt >= now.AddHours(-1), cancellationToken);
        if (recentCreations >= hourlyLimit)
            return new FamilyCreateResult(FamilyCreateOutcome.Throttled, null);

        var family = new Family(request.Name!.Trim(), request.Description?.Trim(), user.Id, now);
        db.Families.Add(family);
        db.FamilyMemberships.Add(new FamilyMembership(family.Id, user.Id, now));
        db.RoleAssignments.AddRange(
            new RoleAssignment(family.Id, user.Id, "CREATOR", user.Id, now),
            new RoleAssignment(family.Id, user.Id, "FAMILY_ADMIN", user.Id, now));
        auditWriter.Add(new AuditEvent(user.Id, "FAMILY_CREATED", "FAMILY", family.Id, requestId, now,
            family.Id, safeDiff: safeDiff));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FamilyCreateResult(FamilyCreateOutcome.Created,
            await GetAsync(firebaseUid, family.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<FamilySummary>> ListAsync(string firebaseUid, CancellationToken cancellationToken)
    {
        var familyIds = await (from user in db.Users
            join membership in db.FamilyMemberships on user.Id equals membership.UserId
            join family in db.Families on membership.FamilyId equals family.Id
            where user.FirebaseUid == firebaseUid && membership.Status == "ACTIVE" && family.Status == "ACTIVE"
            select family.Id).ToListAsync(cancellationToken);
        var result = new List<FamilySummary>(familyIds.Count);
        foreach (var id in familyIds)
        {
            var summary = await GetAsync(firebaseUid, id, cancellationToken);
            if (summary is not null) result.Add(summary);
        }
        return result.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Id).ToArray();
    }

    public async Task<FamilySummary?> GetAsync(string firebaseUid, Guid familyId, CancellationToken cancellationToken)
    {
        var row = await (from user in db.Users.AsNoTracking()
            join membership in db.FamilyMemberships.AsNoTracking() on user.Id equals membership.UserId
            join family in db.Families.AsNoTracking() on membership.FamilyId equals family.Id
            where user.FirebaseUid == firebaseUid && membership.Status == "ACTIVE" &&
                family.Id == familyId && family.Status == "ACTIVE"
            select new { family.Id, family.Name, family.Description, family.Version, UserId = user.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var roles = await db.RoleAssignments.AsNoTracking()
            .Where(r => r.UserId == row.UserId && r.FamilyId == familyId && r.RevokedAt == null)
            .Select(r => r.Role).ToArrayAsync(cancellationToken);
        if (roles.Length == 0) return null;
        var capabilities = new List<string> { "READ_FAMILY" };
        if (roles.Contains("FAMILY_ADMIN") || roles.Contains("CREATOR"))
        {
            capabilities.Add("EDIT_FAMILY");
            capabilities.Add("MANAGE_MEMBER_ROLES");
        }
        if (roles.Contains("CREATOR"))
        {
            capabilities.Add("MANAGE_MEMBERS");
            capabilities.Add("MANAGE_ROLES");
        }
        return new FamilySummary(row.Id, row.Name, row.Description, row.Version, roles, capabilities);
    }

    public async Task<RoleChangeOutcome> ChangeRoleAsync(string firebaseUid, Guid familyId, Guid memberId,
        ChangeFamilyRoleRequest request, string requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var family = await db.Families.FromSqlInterpolated($"SELECT * FROM families WHERE id = {familyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (family is null || family.Status != "ACTIVE") return RoleChangeOutcome.NotFound;

        var actorId = await db.Users.Where(u => u.FirebaseUid == firebaseUid)
            .Select(u => (Guid?)u.Id).SingleOrDefaultAsync(cancellationToken);
        if (actorId is null) return RoleChangeOutcome.NotFound;
        var actorActive = await db.FamilyMemberships.AnyAsync(m => m.FamilyId == familyId &&
            m.UserId == actorId && m.Status == "ACTIVE", cancellationToken);
        if (!actorActive) return RoleChangeOutcome.NotFound;

        var actorRoles = await db.RoleAssignments.Where(r => r.FamilyId == familyId &&
            r.UserId == actorId && r.RevokedAt == null).Select(r => r.Role).ToArrayAsync(cancellationToken);
        var canManage = request.Role == "FAMILY_MEMBER"
            ? actorRoles.Contains("FAMILY_ADMIN") || actorRoles.Contains("CREATOR")
            : actorRoles.Contains("CREATOR");
        if (!canManage) return RoleChangeOutcome.Forbidden;

        var member = await db.FamilyMemberships.SingleOrDefaultAsync(m => m.Id == memberId &&
            m.FamilyId == familyId && m.Status == "ACTIVE", cancellationToken);
        if (member is null) return RoleChangeOutcome.NotFound;
        if (request.Grant == true && member.UserId == actorId) return RoleChangeOutcome.Forbidden;

        var assignment = await db.RoleAssignments.SingleOrDefaultAsync(r => r.FamilyId == familyId &&
            r.UserId == member.UserId && r.Role == request.Role && r.RevokedAt == null, cancellationToken);
        if (request.Grant == true)
        {
            if (assignment is null)
            {
                assignment = new RoleAssignment(familyId, member.UserId, request.Role!, actorId.Value,
                    clock.GetUtcNow());
                db.RoleAssignments.Add(assignment);
            }
        }
        else
        {
            if (assignment is null) return RoleChangeOutcome.Changed;
            if (request.Role == "CREATOR")
            {
                var activeCreatorCount = await (from role in db.RoleAssignments
                    join membership in db.FamilyMemberships on new { role.FamilyId, role.UserId }
                        equals new { FamilyId = (Guid?)membership.FamilyId, membership.UserId }
                    where role.FamilyId == familyId && role.Role == "CREATOR" && role.RevokedAt == null &&
                        membership.Status == "ACTIVE"
                    select role.Id).CountAsync(cancellationToken);
                if (activeCreatorCount <= 1) return RoleChangeOutcome.Conflict;
            }
            assignment.Revoke(clock.GetUtcNow(), null);
        }

        auditWriter.Add(new AuditEvent(actorId, request.Grant == true ? "FAMILY_ROLE_GRANTED" : "FAMILY_ROLE_REVOKED",
            "ROLE_ASSIGNMENT", assignment.Id, requestId, clock.GetUtcNow(), familyId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoleChangeOutcome.Changed;
    }

    public async Task<FamilyUpdateResult> UpdateAsync(string firebaseUid, Guid familyId, int expectedVersion,
        UpdateFamilyRequest request, string requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var family = await db.Families.FromSqlInterpolated($"SELECT * FROM families WHERE id = {familyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (family is null || family.Status != "ACTIVE")
            return new FamilyUpdateResult(FamilyUpdateOutcome.NotFound, null);
        var actorId = await db.Users.Where(u => u.FirebaseUid == firebaseUid)
            .Select(u => (Guid?)u.Id).SingleOrDefaultAsync(cancellationToken);
        if (actorId is null || !await db.FamilyMemberships.AnyAsync(m => m.FamilyId == familyId &&
            m.UserId == actorId && m.Status == "ACTIVE", cancellationToken))
            return new FamilyUpdateResult(FamilyUpdateOutcome.NotFound, null);
        var allowed = await db.RoleAssignments.AnyAsync(r => r.FamilyId == familyId &&
            r.UserId == actorId && r.RevokedAt == null &&
            (r.Role == "CREATOR" || r.Role == "FAMILY_ADMIN"), cancellationToken);
        if (!allowed) return new FamilyUpdateResult(FamilyUpdateOutcome.Forbidden, null);
        if (family.Version != expectedVersion)
            return new FamilyUpdateResult(FamilyUpdateOutcome.Conflict, null);

        family.UpdateDetails(request.Name!.Trim(), request.Description?.Trim(), actorId.Value, clock.GetUtcNow());
        auditWriter.Add(new AuditEvent(actorId, "FAMILY_UPDATED", "FAMILY", familyId, requestId,
            clock.GetUtcNow(), familyId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FamilyUpdateResult(FamilyUpdateOutcome.Updated,
            await GetAsync(firebaseUid, familyId, cancellationToken));
    }
}
