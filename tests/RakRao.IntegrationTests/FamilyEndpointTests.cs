using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using RakRao.Domain.Families;

namespace RakRao.IntegrationTests;

public sealed class FamilyEndpointTests
{
    private static WebApplicationFactory<Program> Factory() => FirestoreFixture.Factory();

    [Fact]
    [Trait("Category", "Database")]
    public async Task Creator_immediately_gets_scoped_family_capabilities_and_other_user_has_none()
    {
        using var factory = Factory();

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
        var db = checkScope.ServiceProvider.GetRequiredService<FirestoreDb>();
        Assert.Equal(1, await FirestoreFixture.Count(db, "audit_events", "familyId", family.Id.ToString(), "action", "FAMILY_CREATED"));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Admin_in_one_family_cannot_manage_roles_in_another_family_where_only_a_member()
    {
        using var factory = Factory();
        using var admin = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var owner = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var adminUser = (await admin.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var ownedByAdmin = await CreateFamily(admin, "Family A");
        var ownedByOther = await CreateFamily(owner, "Family B");
        var ownerUser = (await owner.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        Guid memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FirestoreDb>();
            var membership = new FamilyMembership(ownedByOther.Id, adminUser.Id, DateTimeOffset.UtcNow);
            memberId = membership.Id;
            await FirestoreFixture.SeedMembership(db, membership);
            await FirestoreFixture.SeedRole(db, new RoleAssignment(ownedByOther.Id, adminUser.Id, "FAMILY_MEMBER", ownerUser.Id, DateTimeOffset.UtcNow));
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
        using var owner = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var stranger = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var family = await CreateFamily(owner, "Members only");
        var ownerId = (await owner.GetFromJsonAsync<UserResponse>("/api/v1/me"))!.Id;
        var strangerId = (await stranger.GetFromJsonAsync<UserResponse>("/api/v1/me"))!.Id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FirestoreDb>();
            await FirestoreFixture.SeedRole(db, new RoleAssignment(family.Id, strangerId, "CREATOR", ownerId, DateTimeOffset.UtcNow));
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
        using var first = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        using var second = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var firstUser = (await first.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var secondUser = (await second.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var family = await CreateFamily(first, "Two creators");
        var firstMembershipId = await MembershipId(factory, family.Id, firstUser.Id);
        Guid secondMembershipId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FirestoreDb>();
            var membership = new FamilyMembership(family.Id, secondUser.Id, DateTimeOffset.UtcNow);
            secondMembershipId = membership.Id;
            await FirestoreFixture.SeedMembership(db, membership);
            await FirestoreFixture.SeedRole(db, new RoleAssignment(family.Id, secondUser.Id, "FAMILY_MEMBER", firstUser.Id, DateTimeOffset.UtcNow));
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
        var checkDb = checkScope.ServiceProvider.GetRequiredService<FirestoreDb>();
        Assert.Equal(1, await FirestoreFixture.Count(checkDb, "active_role_keys", "familyId", family.Id.ToString(), "role", "CREATOR"));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Role_change_requires_explicit_grant_flag()
    {
        using var factory = Factory();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var user = (await creator.GetFromJsonAsync<UserResponse>("/api/v1/me"))!;
        var family = await CreateFamily(creator, "Explicit role action");
        var membershipId = await MembershipId(factory, family.Id, user.Id);

        using var response = await creator.PatchAsJsonAsync(
            $"/api/v1/families/{family.Id}/members/{membershipId}/roles",
            new { role = "CREATOR" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<FirestoreDb>();
        Assert.Equal(1, await FirestoreFixture.Count(db, "active_role_keys", "familyId", family.Id.ToString(), "role", "CREATOR"));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Family_creation_is_throttled_per_authenticated_user()
    {
        using var factory = Factory();
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
        using var secondFactory = FirestoreFixture.Factory(firstFactory.Services.GetRequiredService<FirestoreDb>().ProjectId);
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
    public async Task Retrying_family_creation_with_same_key_returns_same_family_once()
    {
        using var factory = Factory();
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
        var db = checkScope.ServiceProvider.GetRequiredService<FirestoreDb>();
        Assert.Equal(1, await FirestoreFixture.Count(db, "audit_events", "familyId", firstFamily.Id.ToString(), "action", "FAMILY_CREATED"));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Reusing_family_creation_key_for_different_details_is_rejected()
    {
        using var factory = Factory();
        using var creator = await ClientFor(factory, $"test-{Guid.NewGuid():N}");
        var key = Guid.NewGuid().ToString("N");
        using var first = await PostFamilyWithKey(creator, key, "First name");
        using var reused = await PostFamilyWithKey(creator, key, "Different name");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var family = (await first.Content.ReadFromJsonAsync<FamilyResponse>())!;
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<FirestoreDb>();
        Assert.Equal(1, await FirestoreFixture.Count(db, "audit_events", "familyId", family.Id.ToString(), "action", "FAMILY_CREATED"));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Idempotent_retry_still_works_after_hourly_limit_is_reached()
    {
        using var factory = Factory();
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

    private static async Task<FamilyResponse> CreateFamily(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/families", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FamilyResponse>())!;
    }

    private static async Task<Guid> MembershipId(WebApplicationFactory<Program> factory, Guid familyId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FirestoreDb>();
        return Guid.Parse((await db.Collection("family_memberships").Document($"{familyId}_{userId}").GetSnapshotAsync()).GetValue<string>("id"));
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

    private sealed record FamilyResponse(Guid Id, string Name, int Version, string[] Capabilities);
    private sealed record MeResponse(FamilyResponse[] Families);
    private sealed record UserResponse(Guid Id);

}
