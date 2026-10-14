using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

public sealed class FirestoreMappingTests
{
    [Fact]
    public async Task Stored_documents_round_trip_distinct_uuids_explicit_nulls_utc_instants_and_integer_versions()
    {
        var instant = new DateTimeOffset(2026, 10, 14, 19, 23, 45, TimeSpan.FromHours(7));
        using var factory = FirestoreFixture.Factory(clock: new FirestoreBehaviorTests.ManualClock(instant));
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        const string uid = "test-firebase-identity-not-an-application-uuid";
        var user = await services.GetRequiredService<IMeService>().ProvisionAsync(new(uid, null), "mapping-user", default);
        var family = (await services.GetRequiredService<IFamilyService>().CreateAsync(uid,
            new("Mapping family", null), null, "mapping-family", default)).Family!;
        var db = services.GetRequiredService<FirestoreDb>();

        var storedUser = await db.Document($"users/{user.Id}").GetSnapshotAsync();
        Assert.Equal(user.Id.ToString("D"), storedUser.GetValue<string>("id"));
        Assert.Equal(uid, storedUser.GetValue<string>("firebaseUid"));
        Assert.NotEqual(uid, storedUser.GetValue<string>("id"));
        Assert.Null(user.DisplayName);
        AssertNullFields(storedUser, "displayName", "createdByUserId", "updatedByUserId");
        AssertVersion(storedUser);
        AssertInstants(storedUser, instant, "createdAt", "updatedAt");
        var mapping = await db.Document(FirestoreDocuments.UidPath(uid)).GetSnapshotAsync();
        Assert.Equal(user.Id.ToString("D"), mapping.GetValue<string>("userId"));

        var storedFamily = await db.Document($"families/{family.Id}").GetSnapshotAsync();
        Assert.Equal(family.Id.ToString("D"), storedFamily.GetValue<string>("id"));
        Assert.NotEqual(user.Id, family.Id);
        Assert.Equal(user.Id.ToString("D"), storedFamily.GetValue<string>("createdByUserId"));
        Assert.Null(family.Description);
        AssertNullFields(storedFamily, "description");
        AssertVersion(storedFamily);
        AssertInstants(storedFamily, instant, "createdAt", "updatedAt");

        var membership = await db.Document(FirestoreDocuments.MembershipPath(family.Id, user.Id)).GetSnapshotAsync();
        Assert.Equal(Guid.Parse(membership.GetValue<string>("id")).ToString("D"), membership.GetValue<string>("id"));
        Assert.Equal(family.Id.ToString("D"), membership.GetValue<string>("familyId"));
        Assert.Equal(user.Id.ToString("D"), membership.GetValue<string>("userId"));
        AssertNullFields(membership, "leftAt", "createdByUserId", "updatedByUserId");
        AssertVersion(membership);
        AssertInstants(membership, instant, "joinedAt", "createdAt", "updatedAt");

        var roles = await db.Collection("role_assignments").GetSnapshotAsync();
        Assert.Equal(2, roles.Count);
        foreach (var role in roles)
        {
            Assert.Equal(Guid.Parse(role.Id).ToString("D"), role.GetValue<string>("id"));
            Assert.Equal(family.Id.ToString("D"), role.GetValue<string>("familyId"));
            Assert.Equal(user.Id.ToString("D"), role.GetValue<string>("userId"));
            AssertNullFields(role, "revokedAt", "reason");
            AssertInstants(role, instant, "grantedAt");
        }

        var audit = Assert.Single((await db.Collection("audit_events").WhereEqualTo("action", "USER_PROVISIONED").GetSnapshotAsync()).Documents);
        Assert.Equal(Guid.Parse(audit.Id).ToString("D"), audit.GetValue<string>("id"));
        Assert.Equal(user.Id.ToString("D"), audit.GetValue<string>("targetId"));
        AssertNullFields(audit, "familyId", "reason", "safeDiff");
        AssertInstants(audit, instant, "occurredAt");
    }

    private static void AssertNullFields(DocumentSnapshot document, params string[] fields)
    {
        var data = document.ToDictionary();
        foreach (var field in fields)
        {
            Assert.True(data.ContainsKey(field), $"{document.Reference.Path} must retain explicit {field}.");
            Assert.Null(data[field]);
        }
    }

    private static void AssertVersion(DocumentSnapshot document)
    {
        Assert.Equal(1L, Assert.IsType<long>(document.ToDictionary()["version"]));
        Assert.Equal(1, document.GetValue<int>("version"));
    }

    private static void AssertInstants(DocumentSnapshot document, DateTimeOffset instant, params string[] fields)
    {
        foreach (var field in fields)
        {
            var stored = Assert.IsType<Timestamp>(document.ToDictionary()[field]).ToDateTimeOffset();
            Assert.Equal(TimeSpan.Zero, stored.Offset);
            Assert.Equal(instant.ToUniversalTime(), stored);
        }
    }
}
