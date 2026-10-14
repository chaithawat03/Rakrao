using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RakRao.Application.Identity;
using RakRao.Domain.Families;

namespace RakRao.IntegrationTests;

internal static class FirestoreFixture
{
    public static FirestoreDb Database(string? project = null)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")))
            throw new InvalidOperationException("FIRESTORE_EMULATOR_HOST must name a running emulator; integration tests never skip or use production.");
        return new FirestoreDbBuilder { ProjectId = project ?? $"demo-rakrao-{Guid.NewGuid():N}", EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    }

    public static WebApplicationFactory<Program> Factory(string? project = null, ILoggerProvider? logger = null,
        TimeProvider? clock = null)
    {
        var db = Database(project);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Firestore:ProjectId"] = db.ProjectId, ["Firebase:ProjectId"] = "demo-rakrao" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<FirestoreDb>(); services.AddSingleton(db);
                services.RemoveAll<IFirebaseIdTokenVerifier>(); services.AddSingleton<IFirebaseIdTokenVerifier>(new FakeVerifier());
                if (logger is not null) services.AddLogging(logging => logging.AddProvider(logger));
                if (clock is not null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(clock); }
            });
        });
    }

    public static async Task<int> Count(FirestoreDb db, string collection, params string[] fields)
    {
        Query query = db.Collection(collection);
        for (var i = 0; i < fields.Length; i += 2) query = query.WhereEqualTo(fields[i], fields[i + 1]);
        return (await query.GetSnapshotAsync()).Count;
    }

    public static Task SeedMembership(FirestoreDb db, FamilyMembership member) => db.Collection("family_memberships")
        .Document($"{member.FamilyId}_{member.UserId}").CreateAsync(new Dictionary<string, object?>
        { ["id"] = member.Id.ToString(), ["familyId"] = member.FamilyId.ToString(), ["userId"] = member.UserId.ToString(),
            ["status"] = member.Status, ["version"] = member.Version, ["joinedAt"] = Timestamp.FromDateTimeOffset(member.JoinedAt) });

    public static async Task SeedRole(FirestoreDb db, RoleAssignment role)
    {
        var data = new Dictionary<string, object?> { ["id"] = role.Id.ToString(), ["familyId"] = role.FamilyId.ToString(),
            ["userId"] = role.UserId.ToString(), ["role"] = role.Role, ["revokedAt"] = null,
            ["grantedAt"] = Timestamp.FromDateTimeOffset(role.GrantedAt), ["grantedByUserId"] = role.GrantedByUserId.ToString() };
        var batch = db.StartBatch();
        batch.Create(db.Collection("role_assignments").Document(role.Id.ToString()), data);
        batch.Create(db.Collection("active_role_keys").Document($"{role.FamilyId}_{role.UserId}_{role.Role}"), data);
        await batch.CommitAsync();
    }

    private sealed class FakeVerifier : IFirebaseIdTokenVerifier
    {
        public Task<VerifiedFirebaseUser?> VerifyAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult<VerifiedFirebaseUser?>(token.StartsWith("test-", StringComparison.Ordinal)
                ? new VerifiedFirebaseUser(token, "Test User") : null);
    }
}
