using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using RakRao.Application.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

[Collection("PostgreSQL")]
public class MeEndpointTests
{
    private static WebApplicationFactory<Program> Factory(string? connectionString = null, ILoggerProvider? loggerProvider = null) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFirebaseIdTokenVerifier>();
                services.AddSingleton<IFirebaseIdTokenVerifier>(new FakeVerifier());
                if (loggerProvider is not null) services.AddLogging(logging => logging.AddProvider(loggerProvider));
                if (connectionString is not null)
                {
                    services.RemoveAll<DbContextOptions<RakRaoDbContext>>();
                    services.AddDbContext<RakRaoDbContext>(options => options.UseNpgsql(connectionString));
                }
            });
        });

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
    public void Test_factory_uses_its_explicit_database_connection()
    {
        const string connectionString = "Host=127.0.0.1;Port=5441;Database=rk_factory_test;Username=test_user;Password=test_password";
        using var factory = Factory(connectionString);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        var actual = new NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);

        Assert.True(actual.Database == "rk_factory_test" && actual.Port == 5441 &&
            actual.Username == "test_user" && actual.Password == "test_password",
            "The API test factory did not use its supplied database connection string.");
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
        var connectionString = Environment.GetEnvironmentVariable("RAKRAO_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        using var factory = Factory(connectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RakRaoDbContext>().Database.MigrateAsync();

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
        var db = checkScope.ServiceProvider.GetRequiredService<RakRaoDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(u => u.FirebaseUid == uid));
        Assert.Equal(0, await db.Users.CountAsync(u => u.FirebaseUid == "spoofed-uid"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.TargetId == user.Id && a.Action == "USER_PROVISIONED"));
        Assert.Equal("provision-check", await db.AuditEvents.Where(a => a.TargetId == user.Id)
            .Select(a => a.RequestId).SingleAsync());
    }

    private sealed class FakeVerifier : IFirebaseIdTokenVerifier
    {
        public Task<VerifiedFirebaseUser?> VerifyAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult<VerifiedFirebaseUser?>(token.StartsWith("test-", StringComparison.Ordinal)
                ? new VerifiedFirebaseUser(token, "Test User") : null);
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
