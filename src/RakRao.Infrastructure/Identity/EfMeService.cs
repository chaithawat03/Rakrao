using Microsoft.EntityFrameworkCore;
using Npgsql;
using RakRao.Application.Audit;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Domain.Audit;
using RakRao.Domain.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure.Identity;

public sealed class EfMeService(RakRaoDbContext db, IAuditWriter auditWriter, IFamilyService families,
    TimeProvider clock) : IMeService
{
    public async Task<MeResponse?> GetAsync(string firebaseUid, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.FirebaseUid == firebaseUid, cancellationToken);
        return user is null ? null : await ToResponseAsync(user, cancellationToken);
    }

    public async Task<MeResponse> ProvisionAsync(VerifiedFirebaseUser firebaseUser, string requestId,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(firebaseUser.Uid, cancellationToken);
        if (existing is not null) return existing;

        var user = new User(firebaseUser.Uid, firebaseUser.DisplayName, clock.GetUtcNow());
        db.Users.Add(user);
        auditWriter.Add(new AuditEvent(user.Id, "USER_PROVISIONED", "USER", user.Id, requestId, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return await ToResponseAsync(user, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_firebase_uid" })
        {
            db.ChangeTracker.Clear();
            return await GetAsync(firebaseUser.Uid, cancellationToken)
                ?? throw new InvalidOperationException("A concurrent User provision was not visible.");
        }
    }

    private async Task<MeResponse> ToResponseAsync(User user, CancellationToken cancellationToken)
    {
        var joined = await families.ListAsync(user.FirebaseUid, cancellationToken);
        return new MeResponse(user.Id, user.DisplayName, joined.Count == 0 ? "NEW_MEMBER" : "ACTIVE_MEMBER", joined);
    }
}
