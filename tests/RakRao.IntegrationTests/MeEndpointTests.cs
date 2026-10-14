using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RakRao.IntegrationTests;

public class MeEndpointTests
{
    private static WebApplicationFactory<Program> Factory(string? projectId = null, ILoggerProvider? loggerProvider = null) => FirestoreFixture.Factory(projectId, loggerProvider);

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("expired")]
    [InlineData("wrong-project")]
    public async Task Protected_me_rejects_missing_or_invalid_credentials(string? token)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Request-ID", "me-auth-check");

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("me-auth-check", response.Headers.GetValues("X-Request-ID").Single());
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("AUTHENTICATION_REQUIRED", problem?.Code);
        Assert.Equal("me-auth-check", problem?.RequestId);
    }

    [Fact]
    public void Test_factory_uses_its_explicit_firestore_project()
    {
        using var factory = Factory("demo-rakrao-explicit");
        using var scope = factory.Services.CreateScope();
        Assert.Equal("demo-rakrao-explicit", scope.ServiceProvider.GetRequiredService<FirestoreDb>().ProjectId);
    }

    [Fact]
    public async Task Invalid_bearer_token_is_logged_without_token_content()
    {
        var logs = new CaptureLoggerProvider();
        using var factory = Factory(loggerProvider: logs);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "private-token-marker");

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(logs.Messages, message => message.Contains("Firebase authentication rejected", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-token-marker", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Provisioning_is_idempotent_and_writes_one_audit_event()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var uid = $"test-{Guid.NewGuid():N}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", uid);
        client.DefaultRequestHeaders.Add("X-Request-ID", "provision-check");

        using var beforeProvision = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.NotFound, beforeProvision.StatusCode);
        Assert.Equal("USER_NOT_PROVISIONED",
            (await beforeProvision.Content.ReadFromJsonAsync<ProblemResponse>())?.Code);

        var concurrent = await Task.WhenAll(
            client.PostAsync("/api/v1/me", null),
            client.PostAsync("/api/v1/me", null));
        using var first = concurrent[0];
        using var second = concurrent[1];
        using var repeat = await client.PostAsJsonAsync("/api/v1/me", new
        {
            firebaseUid = "spoofed-uid", userId = Guid.NewGuid(), role = "SUPER_ADMIN"
        });
        using var get = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var user = await get.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(user);
        Assert.Equal("NEW_MEMBER", user.OnboardingState);
        Assert.Empty(user.Families);
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<FirestoreDb>();
        Assert.Equal(1, await FirestoreFixture.Count(db, "users", "firebaseUid", uid));
        Assert.Equal(0, await FirestoreFixture.Count(db, "users", "firebaseUid", "spoofed-uid"));
        var audits = await db.Collection("audit_events").WhereEqualTo("targetId", user.Id.ToString()).GetSnapshotAsync();
        Assert.Single(audits);
        Assert.Equal("USER_PROVISIONED", audits[0].GetValue<string>("action"));
        Assert.Equal("provision-check", audits[0].GetValue<string>("requestId"));
        Assert.Single((await db.Collection("firebase_uid_mappings").GetSnapshotAsync()).Documents);
    }

    private sealed record ProblemResponse(string Code, string RequestId);
    private sealed record MeResponse(Guid Id, string OnboardingState, Guid[] Families);

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
