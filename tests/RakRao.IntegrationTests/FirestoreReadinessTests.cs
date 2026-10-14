using System.Diagnostics;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RakRao.Infrastructure;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

public sealed class FirestoreReadinessTests
{
    [Fact]
    public async Task Unreachable_backend_returns_not_ready_within_probe_budget_and_caches_failure()
    {
        using var channel = GrpcChannel.ForAddress("http://127.0.0.1:1");
        var db = FirestoreDb.Create("demo-rakrao-unreachable", new FirestoreClientBuilder { CallInvoker = channel.CreateCallInvoker() }.Build());
        var check = new FirestoreReadiness(db, TimeProvider.System);
        var elapsed = Stopwatch.StartNew();
        Assert.False(await check.CheckAsync(default));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(6), $"Probe took {elapsed.Elapsed}.");
        elapsed.Restart();
        Assert.False(await check.CheckAsync(default));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), "Repeated readiness requests must use the cached result.");
    }

    [Fact]
    public void Runtime_rejects_firestore_emulator_in_production()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")));
        using var host = Host.CreateDefaultBuilder().UseEnvironment(Environments.Production).ConfigureServices(services =>
            services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Firestore:ProjectId"] = "demo-rakrao" }).Build())).Build();
        var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<FirestoreDb>());
        Assert.Equal("Firestore Emulator is allowed only in Development.", error.Message);
    }
}
