using System.Net;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Domain.Families;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

public sealed class FirestoreBehaviorTests
{
    [Fact]
    public async Task Rolling_hour_includes_exact_boundary_and_does_not_reset_at_clock_hour()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 10, 14, 10, 59, 0, TimeSpan.Zero));
        using var factory = FirestoreFixture.Factory(clock: clock);
        using var scope = factory.Services.CreateScope();
        var me = scope.ServiceProvider.GetRequiredService<IMeService>();
        var families = scope.ServiceProvider.GetRequiredService<IFamilyService>();
        const string uid = "test-rolling";
        await me.ProvisionAsync(new(uid, null), "provision", default);
        for (var i = 0; i < 3; i++) Assert.Equal(FamilyCreateOutcome.Created,
            (await families.CreateAsync(uid, new($"Family {i}", null), $"rolling-key-{i}", $"create-{i}", default)).Outcome);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Equal(FamilyCreateOutcome.Throttled, (await families.CreateAsync(uid, new("At clock hour", null), null, "throttle", default)).Outcome);
        clock.Now = clock.Now.AddMinutes(59);
        Assert.Equal(FamilyCreateOutcome.Throttled, (await families.CreateAsync(uid, new("At exact cutoff", null), null, "throttle", default)).Outcome);
        clock.Now = clock.Now.AddTicks(10); // Firestore timestamp precision is one microsecond.
        Assert.Equal(FamilyCreateOutcome.Created, (await families.CreateAsync(uid, new("After cutoff", null), null, "released", default)).Outcome);
    }

    [Fact]
    public async Task Inactive_membership_cannot_read_edit_or_grant_even_with_creator_key()
    {
        using var factory = FirestoreFixture.Factory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var me = services.GetRequiredService<IMeService>();
        var families = services.GetRequiredService<IFamilyService>();
        var db = services.GetRequiredService<FirestoreDb>();
        const string ownerUid = "test-owner";
        const string inactiveUid = "test-inactive";
        var owner = await me.ProvisionAsync(new(ownerUid, null), "owner", default);
        var inactive = await me.ProvisionAsync(new(inactiveUid, null), "inactive", default);
        var family = (await families.CreateAsync(ownerUid, new("Private", null), null, "family", default)).Family!;
        var member = new FamilyMembership(family.Id, inactive.Id, DateTimeOffset.UtcNow);
        await FirestoreFixture.SeedMembership(db, member);
        await FirestoreFixture.SeedRole(db, new RoleAssignment(family.Id, inactive.Id, "CREATOR", owner.Id, DateTimeOffset.UtcNow));
        await db.Document(FirestoreDocuments.MembershipPath(family.Id, inactive.Id)).UpdateAsync("status", "INACTIVE");
        Assert.Null(await families.GetAsync(inactiveUid, family.Id, default));
        Assert.Empty(await families.ListAsync(inactiveUid, default));
        Assert.Equal(FamilyUpdateOutcome.NotFound, (await families.UpdateAsync(inactiveUid, family.Id, 1, new("Forged", null), "edit", default)).Outcome);
        Assert.Equal(RoleChangeOutcome.NotFound, await families.ChangeRoleAsync(inactiveUid, family.Id, member.Id, new("FAMILY_ADMIN", true), "grant", default));
        var ownerMember = await db.Document(FirestoreDocuments.MembershipPath(family.Id, owner.Id)).GetSnapshotAsync();
        Assert.Equal(RoleChangeOutcome.Conflict, await families.ChangeRoleAsync(ownerUid, family.Id, ownerMember.Id(), new("CREATOR", false), "revoke", default));
        Assert.Equal(RoleChangeOutcome.NotFound, await families.ChangeRoleAsync(ownerUid, family.Id, member.Id, new("CREATOR", true), "grant-inactive", default));
    }

    [Fact]
    public async Task Concurrent_metadata_writes_allow_one_winner_and_roles_do_not_change_metadata_version()
    {
        using var factory = FirestoreFixture.Factory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var me = services.GetRequiredService<IMeService>();
        var families = services.GetRequiredService<IFamilyService>();
        var db = services.GetRequiredService<FirestoreDb>();
        var owner = await me.ProvisionAsync(new("test-owner", null), "owner", default);
        var other = await me.ProvisionAsync(new("test-other", null), "other", default);
        var family = (await families.CreateAsync("test-owner", new("Concurrent", null), null, "family", default)).Family!;
        var member = new FamilyMembership(family.Id, other.Id, DateTimeOffset.UtcNow);
        await FirestoreFixture.SeedMembership(db, member);
        Assert.Equal(RoleChangeOutcome.Changed, await families.ChangeRoleAsync("test-owner", family.Id, member.Id, new("CREATOR", true), "grant", default));
        Assert.Equal(1, (await families.GetAsync("test-owner", family.Id, default))!.Version);
        var updates = await Task.WhenAll(families.UpdateAsync("test-owner", family.Id, 1, new("First", null), "first", default),
            families.UpdateAsync("test-owner", family.Id, 1, new("Second", null), "second", default));
        Assert.Single(updates, r => r.Outcome == FamilyUpdateOutcome.Updated);
        Assert.Single(updates, r => r.Outcome == FamilyUpdateOutcome.Conflict);
        Assert.Equal(2, (await db.Document($"family_guards/{family.Id}").GetSnapshotAsync()).GetValue<int>("version"));
        Assert.Equal(1, await FirestoreFixture.Count(db, "audit_events", "familyId", family.Id.ToString(), "action", "FAMILY_UPDATED"));
    }

    [Fact]
    public async Task Concurrent_same_key_creates_one_family_and_distinct_identities_stay_distinct()
    {
        using var factory = FirestoreFixture.Factory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var me = services.GetRequiredService<IMeService>();
        var families = services.GetRequiredService<IFamilyService>();
        var owner = await me.ProvisionAsync(new("test-one", "Same display name"), "one", default);
        var other = await me.ProvisionAsync(new("test-two", "Same display name"), "two", default);
        Assert.NotEqual(owner.Id, other.Id);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => families.CreateAsync("test-one",
            new(" Family ", null), "same-key", $"request-{i}", default)));
        Assert.All(results, r => Assert.Equal(FamilyCreateOutcome.Created, r.Outcome));
        Assert.Single(results.Select(r => r.Family!.Id).Distinct());
        var db = services.GetRequiredService<FirestoreDb>();
        Assert.Single((await db.Collection("families").GetSnapshotAsync()).Documents);
        Assert.Equal(1, await FirestoreFixture.Count(db, "audit_events", "action", "FAMILY_CREATED"));
        Assert.Equal(2, (await db.Collection("users").GetSnapshotAsync()).Count);
        Assert.Equal(2, (await db.Collection("firebase_uid_mappings").GetSnapshotAsync()).Count);
    }

    [Fact]
    public async Task Readiness_returns_ready_for_missing_probe_document_without_writes()
    {
        using var factory = FirestoreFixture.Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await factory.Services.GetRequiredService<FirestoreDb>().Collection("readiness").GetSnapshotAsync()).Documents);
    }

    internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
