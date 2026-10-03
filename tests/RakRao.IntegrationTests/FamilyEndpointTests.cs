using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RakRao.Application.Identity;
using RakRao.Application.Audit;
using RakRao.Domain.Audit;
using RakRao.Domain.Families;
using RakRao.Domain.Identity;
using RakRao.Infrastructure.Families;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class FamilyEndpointTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFirebaseIdTokenVerifier>();
            services.AddSingleton<IFirebaseIdTokenVerifier>(new FakeVerifier());
            services.RemoveAll<DbContextOptions<RakRaoDbContext>>();
            services.AddDbContext<RakRaoDbContext>(options => options.UseNpgsql(
                Environment.GetEnvironmentVariable("RAKRAO_TEST_CONNECTION_STRING")));
        }));

    [Fact]
    [Trait("Category", "Database")]
    public async Task Creator_immediately_gets_scoped_family_capabilities_and_other_user_has_none()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();

        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var outsider = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var created = await creator.PostAsJsonAsync("/api/v1/families", new { name = "Our family", description = "A private space" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var family = await created.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.NotNull(family);
        Assert.Equal("Our family", family.Name);
        Assert.Contains("MANAGE_ROLES", family.Capabilities);

        using var mine = await creator.GetAsync($"/api/v1/families/{family.Id}");
        using var theirs = await outsider.GetAsync($"/api/v1/families/{family.Id}");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);

        using var updated = await PatchFamily(creator, family.Id, family.Version,
            "Our updated family", "New description");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Our updated family", (await updated.Content.ReadFromJsonAsync<FamilyResponse>())!.Name);
        using var stale = await PatchFamily(creator, family.Id, family.Version,
            "Stale overwrite", "Should not be saved");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var me = await creator.GetFromJsonAsync<MeResponse>("/api/v1/me");
        Assert.Contains(me!.Families, item => item.Id == family.Id);
        var otherMe = await outsider.GetFromJsonAsync<MeResponse>("/api/v1/me");
        Assert.Empty(otherMe!.Families);

        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "FAMILY_CREATED" && a.FamilyId == family.Id));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Admin_in_one_family_cannot_manage_roles_in_another_family_where_only_a_member()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var admin = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var owner = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var adminUser = (await admin.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var ownedByAdmin = await CreateFamily(admin, "Family A");
        var ownedByOther = await CreateFamily(owner, "Family B");
        var ownerUser = (await owner.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        Guid memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
            var membership = new FamilyMembership(ownedByOther.Id, adminUser.Id, DateTimeOffset.UtcNow);
            memberId = membership.Id;
            db.FamilyMemberships.Add(membership);
            db.RoleAssignments.Add(new RoleAssignment(ownedByOther.Id, adminUser.Id, "FAMILY_MEMBER", ownerUser.Id, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        using var visible = await admin.GetAsync($"/api/v1/families/{ownedByOther.Id}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        var summary = await visible.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.DoesNotContain("MANAGE_ROLES", summary!.Capabilities);
        using var forbidden = await admin.PatchAsJsonAsync(
            $"/api/v1/families/{ownedByOther.Id}/members/{memberId}/roles",
            new { role = "CREATOR", grant = true });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenEdit = await PatchFamily(admin, ownedByOther.Id, ownedByOther.Version,
            "Forged edit", "");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEdit.StatusCode);
        Assert.Contains(ownedByAdmin.Id, (await admin.GetFromJsonAsync<MeResponse>("/api/v1/me"))!.Families.Select(f => f.Id));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task A_grant_without_active_membership_gives_no_family_access()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var owner = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var stranger = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var family = await CreateFamily(owner, "Members only");
        var ownerId = (await owner.GetFromJsonAsync<UserResponse>("/api/v1/me"))!.Id;
        var strangerId = (await stranger.GetFromJsonAsync<UserResponse>("/api/v1/me"))!.Id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
            db.RoleAssignments.Add(new RoleAssignment(family.Id, strangerId, "CREATOR", ownerId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        using var response = await stranger.GetAsync($"/api/v1/families/{family.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await stranger.GetFromJsonAsync<MeResponse>("/api/v1/me"))!.Families);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Concurrent_creator_revocations_cannot_remove_both_creators()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var first = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var second = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var firstUser = (await first.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var secondUser = (await second.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var family = await CreateFamily(first, "Two creators");
        var firstMembershipId = await MembershipId(factory, family.Id, firstUser.Id);
        Guid secondMembershipId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
            var membership = new FamilyMembership(family.Id, secondUser.Id, DateTimeOffset.UtcNow);
            secondMembershipId = membership.Id;
            db.FamilyMemberships.Add(membership);
            db.RoleAssignments.Add(new RoleAssignment(family.Id, secondUser.Id, "FAMILY_MEMBER", firstUser.Id, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        using (var granted = await first.PatchAsJsonAsync(
            $"/api/v1/families/{family.Id}/members/{secondMembershipId}/roles",
            new { role = "CREATOR", grant = true }))
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        var responses = await Task.WhenAll(
            first.PatchAsJsonAsync($"/api/v1/families/{family.Id}/members/{firstMembershipId}/roles", new { role = "CREATOR", grant = false }),
            second.PatchAsJsonAsync($"/api/v1/families/{family.Id}/members/{secondMembershipId}/roles", new { role = "CREATOR", grant = false }));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await using var checkScope = factory.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        Assert.Equal(1, await checkDb.RoleAssignments.CountAsync(r =>
            r.FamilyId == family.Id && r.Role == "CREATOR" && r.RevokedAt == null));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Role_change_requires_explicit_grant_flag()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var user = (await creator.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var family = await CreateFamily(creator, "Explicit role action");
        var membershipId = await MembershipId(factory, family.Id, user.Id);

        using var response = await creator.PatchAsJsonAsync(
            $"/api/v1/families/{family.Id}/members/{membershipId}/roles",
            new { role = "CREATOR" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        Assert.Equal(1, await db.RoleAssignments.CountAsync(r => r.FamilyId == family.Id &&
            r.Role == "CREATOR" && r.RevokedAt == null));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Database_failure_during_audit_write_rolls_back_family_creation()
    {
        var connectionString = Environment.GetEnvironmentVariable("RAKRAO_TEST_CONNECTION_STRING");
        var options = new DbContextOptionsBuilder<RakRaoDbContext>().UseNpgsql(connectionString).Options;
        var uid = $"test-{Guid.NewGuid():N}";
        var name = $"Rollback {Guid.NewGuid():N}";
        await using var db = new RakRaoDbContext(options);
        await db.Database.MigrateAsync();
        db.Users.Add(new User(uid, "Test", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var service = new EfFamilyService(db, new CorruptAuditWriter(db), TimeProvider.System,
            new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(uid,
            new(name, null), null, "rollback-check", CancellationToken.None));

        await using var check = new RakRaoDbContext(options);
        Assert.Equal(0, await check.Families.CountAsync(f => f.Name == name));
        Assert.Equal(0, await check.AuditEvents.CountAsync(a => a.RequestId == "rollback-check"));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Family_creation_is_throttled_per_authenticated_user()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        for (var i = 0; i < 3; i++)
        {
            using var allowed = await creator.PostAsJsonAsync("/api/v1/families", new { name = $"Family {i}" });
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        }
        using var limited = await creator.PostAsJsonAsync("/api/v1/families", new { name = "Fourth family" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Family_creation_limit_is_shared_across_api_instances()
    {
        using var firstFactory = Factory();
        using var secondFactory = Factory();
        await using (var scope = firstFactory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        var uid = $"test-{Guid.NewGuid():N}";
        using var first = await ClientFor(firstFactory, uid);
        using var second = await ClientFor(secondFactory, uid);
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/v1/families", new { name = "One" }),
            second.PostAsJsonAsync("/api/v1/families", new { name = "Two" }),
            first.PostAsJsonAsync("/api/v1/families", new { name = "Three" }),
            second.PostAsJsonAsync("/api/v1/families", new { name = "Four" }));
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Direct_database_revocation_cannot_remove_last_active_creator()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var family = await CreateFamily(creator, "Guarded family");
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE role_assignments SET revoked_at = NOW() WHERE family_id = {family.Id} AND role = 'CREATOR'");
        Exception? failure = null;
        try { await transaction.CommitAsync(); }
        catch (Exception exception) { failure = exception; }
        await transaction.DisposeAsync();
        if (failure is null)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE role_assignments SET revoked_at = NULL WHERE family_id = {family.Id} AND role = 'CREATOR'");
        Assert.NotNull(failure);
        Assert.Equal(1, await db.RoleAssignments.CountAsync(r => r.FamilyId == family.Id &&
            r.Role == "CREATOR" && r.RevokedAt == null));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Retrying_family_creation_with_same_key_returns_same_family_once()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostFamilyWithKey(creator, key, "One family");
        using var retry = await PostFamilyWithKey(creator, key, "One family");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var firstFamily = (await first.Content.ReadFromJsonAsync<FamilyResponse>())!;
        var retriedFamily = (await retry.Content.ReadFromJsonAsync<FamilyResponse>())!;
        Assert.Equal(firstFamily.Id, retriedFamily.Id);
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "FAMILY_CREATED" && a.FamilyId == firstFamily.Id));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Reusing_family_creation_key_for_different_details_is_rejected()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostFamilyWithKey(creator, key, "First name");
        using var reused = await PostFamilyWithKey(creator, key, "Different name");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var family = (await first.Content.ReadFromJsonAsync<FamilyResponse>())!;
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "FAMILY_CREATED" &&
            a.FamilyId == family.Id));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Idempotent_retry_still_works_after_hourly_limit_is_reached()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var firstKey = Guid.NewGuid().ToString("N");
        using var first = await PostFamilyWithKey(creator, firstKey, "First family");
        using var second = await PostFamilyWithKey(creator, Guid.NewGuid().ToString("N"), "Second family");
        using var third = await PostFamilyWithKey(creator, Guid.NewGuid().ToString("N"), "Third family");
        using var retry = await PostFamilyWithKey(creator, firstKey, "First family");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<FamilyResponse>())!.Id,
            (await retry.Content.ReadFromJsonAsync<FamilyResponse>())!.Id);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Idempotent_retry_survives_later_family_metadata_edits()
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var key = Guid.NewGuid().ToString("N");
        using var original = await PostFamilyWithKey(creator, key, "Original name");
        var created = (await original.Content.ReadFromJsonAsync<FamilyResponse>())!;
        using var edited = await PatchFamily(creator, created.Id, created.Version, "Edited name", "");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        using var retry = await PostFamilyWithKey(creator, key, "Original name");
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var replayed = (await retry.Content.ReadFromJsonAsync<FamilyResponse>())!;
        Assert.Equal(created.Id, replayed.Id);
        Assert.Equal("Edited name", replayed.Name);
    }

    [Theory]
    [InlineData("role_assignments")]
    [InlineData("family_memberships")]
    [Trait("Category", "Database")]
    public async Task Truncate_cannot_remove_all_active_creators(string table)
    {
        using var factory = Factory();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var family = await CreateFamily(creator, "Truncate guard");
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Exception? failure = null;
        try
        {
            if (table == "role_assignments")
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE role_assignments");
            else
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE family_memberships");
        }
        catch (Exception exception) { failure = exception; }
        await transaction.RollbackAsync();
        Assert.NotNull(failure);
        Assert.Equal(1, await db.RoleAssignments.CountAsync(r => r.FamilyId == family.Id &&
            r.Role == "CREATOR" && r.RevokedAt == null));
    }

    private static async Task<FamilyResponse> CreateFamily(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/families", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FamilyResponse>())!;
    }

    private static async Task<Guid> MembershipId(WebApplicationFactory<Program> factory, Guid familyId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        return await db.FamilyMemberships.Where(m => m.FamilyId == familyId && m.UserId == userId)
            .Select(m => m.Id).SingleAsync();
    }

    private static Task<HttpResponseMessage> PatchFamily(HttpClient client, Guid familyId, int version,
        string name, string description)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/families/{familyId}")
        {
            Content = JsonContent.Create(new { name, description })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostFamilyWithKey(HttpClient client, string key, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/families")
        {
            Content = JsonContent.Create(new { name })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task<HttpClient> ClientFor(WebApplicationFactory<Program> factory, string uid)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", uid);
        using var provision = await client.PostAsync("/api/v1/me", null);
        Assert.Equal(HttpStatusCode.OK, provision.StatusCode);
        return client;
    }

    private sealed class FakeVerifier : IFirebaseIdTokenVerifier
    {
        public Task<VerifiedFirebaseUser?> VerifyAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult<VerifiedFirebaseUser?>(token.StartsWith("test-", StringComparison.Ordinal)
                ? new VerifiedFirebaseUser(token, "Test User") : null);
    }

    private sealed record FamilyResponse(Guid Id, string Name, int Version, string[] Capabilities);
    private sealed record MeResponse(FamilyResponse[] Families);
    private sealed record UserResponse(Guid Id);

    private sealed class CorruptAuditWriter(RakRaoDbContext db) : IAuditWriter
    {
        public void Add(AuditEvent auditEvent) => db.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(), auditEvent.Action, auditEvent.TargetType, auditEvent.TargetId,
            auditEvent.RequestId, auditEvent.OccurredAt, auditEvent.FamilyId));
    }
}
