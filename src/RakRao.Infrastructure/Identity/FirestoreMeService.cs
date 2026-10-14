using Google.Cloud.Firestore;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Domain.Audit;
using RakRao.Domain.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure.Identity;

public sealed class FirestoreMeService(FirestoreDb db, IFirestoreUnitOfWork work, IFamilyService families,
    TimeProvider clock) : IMeService
{
    public async Task<MeResponse?> GetAsync(string firebaseUid, CancellationToken cancellationToken)
    {
        var mapping = await db.Document(FirestoreDocuments.UidPath(firebaseUid)).GetSnapshotAsync(cancellationToken);
        if (!mapping.Exists) return null;
        var user = await db.Document($"users/{mapping.Id("userId")}").GetSnapshotAsync(cancellationToken);
        return await ResponseAsync(user.Id(), user.Text("displayName"), firebaseUid, cancellationToken);
    }

    public async Task<MeResponse> ProvisionAsync(VerifiedFirebaseUser firebaseUser, string requestId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidate = new User(firebaseUser.Uid, firebaseUser.DisplayName, now);
        var audit = new AuditEvent(candidate.Id, "USER_PROVISIONED", "USER", candidate.Id, requestId, now);
        var userId = await work.RunAsync(async tx =>
        {
            var mapping = await tx.ReadAsync(FirestoreDocuments.UidPath(firebaseUser.Uid));
            if (mapping.Exists) return mapping.Id("userId");
            tx.Create($"users/{candidate.Id}", FirestoreDocuments.User(candidate));
            tx.Create(FirestoreDocuments.UidPath(firebaseUser.Uid), new() { ["userId"] = candidate.Id.ToString() });
            tx.Add(audit);
            return candidate.Id;
        }, cancellationToken);
        var user = await db.Document($"users/{userId}").GetSnapshotAsync(cancellationToken);
        return await ResponseAsync(userId, user.Text("displayName"), firebaseUser.Uid, cancellationToken);
    }

    private async Task<MeResponse> ResponseAsync(Guid id, string? displayName, string uid, CancellationToken cancellationToken)
    {
        var joined = await families.ListAsync(uid, cancellationToken);
        return new(id, displayName, joined.Count == 0 ? "NEW_MEMBER" : "ACTIVE_MEMBER", joined);
    }
}
