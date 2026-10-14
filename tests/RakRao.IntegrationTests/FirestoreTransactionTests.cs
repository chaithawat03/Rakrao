using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Domain.Audit;
using RakRao.Infrastructure.Families;
using RakRao.Infrastructure.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

public sealed class FirestoreTransactionTests
{
    [Fact]
    public async Task Family_commit_failure_rolls_back_every_business_audit_limit_and_request_write()
    {
        using var factory = FirestoreFixture.Factory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        const string uid = "test-rollback";
        await services.GetRequiredService<IMeService>().ProvisionAsync(new(uid, null), "setup", default);
        var db = services.GetRequiredService<FirestoreDb>();
        await db.Document("audit_events/collision").CreateAsync(new { marker = "original" });
        var service = new FirestoreFamilyService(db, new CollisionWork(new FirestoreUnitOfWork(db)),
            TimeProvider.System, new ConfigurationBuilder().Build());
        var error = await Assert.ThrowsAsync<RpcException>(() => service.CreateAsync(uid, new("Rollback", null), "rollback-key", "rollback", default));
        Assert.Equal(StatusCode.AlreadyExists, error.StatusCode);
        foreach (var collection in new[] { "families", "family_memberships", "role_assignments", "active_role_keys", "family_guards", "family_creation_limits", "family_creation_requests" })
            Assert.Empty((await db.Collection(collection).GetSnapshotAsync()).Documents);
        Assert.Equal(0, await FirestoreFixture.Count(db, "audit_events", "requestId", "rollback"));
        Assert.Equal("original", (await db.Document("audit_events/collision").GetSnapshotAsync()).GetValue<string>("marker"));
        Assert.Equal(FamilyCreateOutcome.Created, (await services.GetRequiredService<IFamilyService>()
            .CreateAsync(uid, new("Rollback", null), "rollback-key", "after-rollback", default)).Outcome);
    }

    [Fact]
    public async Task Sdk_retries_aborted_commit_without_duplicate_families_grants_or_audit_events()
    {
        using var factory = FirestoreFixture.Factory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        const string uid = "test-retry";
        await services.GetRequiredService<IMeService>().ProvisionAsync(new(uid, null), "setup", default);
        var original = services.GetRequiredService<FirestoreDb>();
        using var channel = GrpcChannel.ForAddress($"http://{Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")}");
        var abort = new AbortFirstCommit();
        var client = new FirestoreClientBuilder { CallInvoker = channel.CreateCallInvoker().Intercept(headers => { headers.Add("authorization", "Bearer owner"); return headers; }).Intercept(abort) }.Build();
        var db = FirestoreDb.Create(original.ProjectId, client);
        var work = new CountingWork(new FirestoreUnitOfWork(db));
        var service = new FirestoreFamilyService(db, work, TimeProvider.System, new ConfigurationBuilder().Build());
        var result = await service.CreateAsync(uid, new("Retry", null), "retry-key", "retry-request", default);
        Assert.Equal(FamilyCreateOutcome.Created, result.Outcome);
        Assert.Equal(2, work.Attempts);
        Assert.Equal(2, abort.Commits);
        Assert.Equal(abort.WritePaths[0], abort.WritePaths[1]);
        Assert.Single((await original.Collection("families").GetSnapshotAsync()).Documents);
        Assert.Single((await original.Collection("family_memberships").GetSnapshotAsync()).Documents);
        Assert.Equal(2, (await original.Collection("role_assignments").GetSnapshotAsync()).Count);
        Assert.Equal(2, (await original.Collection("active_role_keys").GetSnapshotAsync()).Count);
        Assert.Equal(1, await FirestoreFixture.Count(original, "audit_events", "requestId", "retry-request"));
        Assert.Single((await original.Collection("family_creation_requests").GetSnapshotAsync()).Documents);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("set")]
    [InlineData("delete")]
    public async Task Application_unit_of_work_rejects_audit_mutation(string mutation)
    {
        var db = FirestoreFixture.Database();
        var work = new FirestoreUnitOfWork(db);
        var audit = new AuditEvent(null, "ORIGINAL", "USER", Guid.NewGuid(), "audit-guard", DateTimeOffset.UtcNow);
        await work.RunAsync(tx => { tx.Add(audit); return Task.FromResult(true); }, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => work.RunAsync(tx =>
        {
            var path = $"audit_events/{audit.Id}";
            if (mutation == "update") tx.Update(path, new() { ["action"] = "CHANGED" });
            if (mutation == "set") tx.Set(path, new() { ["action"] = "CHANGED" });
            if (mutation == "delete") tx.Delete(path);
            return Task.FromResult(true);
        }, default));
        Assert.Equal("ORIGINAL", (await db.Document($"audit_events/{audit.Id}").GetSnapshotAsync()).GetValue<string>("action"));
    }

    [Fact]
    public async Task Audit_create_cannot_overwrite_existing_event()
    {
        var db = FirestoreFixture.Database();
        var work = new FirestoreUnitOfWork(db);
        var audit = new AuditEvent(null, "ORIGINAL", "USER", Guid.NewGuid(), "audit-create", DateTimeOffset.UtcNow);
        await work.RunAsync(tx => { tx.Add(audit); return Task.FromResult(true); }, default);
        var error = await Assert.ThrowsAsync<RpcException>(() => work.RunAsync(tx => { tx.Add(audit); return Task.FromResult(true); }, default));
        Assert.Equal(StatusCode.AlreadyExists, error.StatusCode);
        Assert.Single((await db.Collection("audit_events").GetSnapshotAsync()).Documents);
    }

    [Fact]
    public async Task Provisioning_commit_failure_leaves_no_user_or_uid_mapping()
    {
        using var factory = FirestoreFixture.Factory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FirestoreDb>();
        await db.Document("audit_events/collision").CreateAsync(new { marker = "original" });
        var service = new FirestoreMeService(db, new CollisionWork(new FirestoreUnitOfWork(db)),
            scope.ServiceProvider.GetRequiredService<IFamilyService>(), TimeProvider.System);
        var error = await Assert.ThrowsAsync<RpcException>(() => service.ProvisionAsync(new("test-rollback", null), "provision", default));
        Assert.Equal(StatusCode.AlreadyExists, error.StatusCode);
        Assert.Empty((await db.Collection("users").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("firebase_uid_mappings").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("audit_events").GetSnapshotAsync()).Documents);
    }

    private sealed class CollisionWork(IFirestoreUnitOfWork inner) : IFirestoreUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<FirestoreUnitOfWork.Attempt, Task<T>> operation, CancellationToken ct) => inner.RunAsync(async tx =>
        {
            var result = await operation(tx);
            tx.Create("audit_events/collision", new() { ["marker"] = "collision" });
            return result;
        }, ct);
    }

    private sealed class CountingWork(IFirestoreUnitOfWork inner) : IFirestoreUnitOfWork
    {
        public int Attempts { get; private set; }
        public Task<T> RunAsync<T>(Func<FirestoreUnitOfWork.Attempt, Task<T>> operation, CancellationToken ct) =>
            inner.RunAsync(tx => { Attempts++; return operation(tx); }, ct);
    }

    // Fault injection at the real SDK transport boundary: reads and successful retry commit use the emulator.
    private sealed class AbortFirstCommit : Interceptor
    {
        public int Commits { get; private set; }
        public List<string[]> WritePaths { get; } = [];
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            if (request is CommitRequest commit) WritePaths.Add(commit.Writes.Select(w => w.Update?.Name ?? w.Delete).ToArray());
            if (context.Method.Name == "Commit" && ++Commits == 1)
            {
                var status = new Status(StatusCode.Aborted, "Deterministic test conflict");
                return new(Task.FromException<TResponse>(new RpcException(status)), Task.FromResult(new Metadata()),
                    () => status, () => new Metadata(), () => { });
            }
            return continuation(request, context);
        }
    }
}
