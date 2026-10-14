using Google.Cloud.Firestore;
using Grpc.Core;

namespace RakRao.Infrastructure.Persistence;

/// <summary>On-demand point read; cache probes for 30 seconds and bound each probe to three seconds.</summary>
public sealed class FirestoreReadiness(FirestoreDb db, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextProbe;
    private bool ready;

    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (clock.GetUtcNow() < nextProbe) return ready;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                // A missing document is a successful backend read, with no write or collection scan.
                await db.Document("readiness/api").GetSnapshotAsync(timeout.Token);
                ready = true;
            }
            catch (RpcException) { ready = false; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { ready = false; }
            nextProbe = clock.GetUtcNow().AddSeconds(30);
            return ready;
        }
        finally { gate.Release(); }
    }
}
