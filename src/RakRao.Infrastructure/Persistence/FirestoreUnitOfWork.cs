using Google.Cloud.Firestore;
using RakRao.Application.Audit;
using RakRao.Domain.Audit;

namespace RakRao.Infrastructure.Persistence;

public interface IFirestoreUnitOfWork
{
    Task<T> RunAsync<T>(Func<FirestoreUnitOfWork.Attempt, Task<T>> operation, CancellationToken cancellationToken);
}

/// <summary>Each SDK retry gets fresh reads and a fresh write buffer. Callers allocate operation IDs before RunAsync.</summary>
public sealed class FirestoreUnitOfWork(FirestoreDb db) : IFirestoreUnitOfWork
{
    public Task<T> RunAsync<T>(Func<Attempt, Task<T>> operation, CancellationToken cancellationToken) =>
        db.RunTransactionAsync(async transaction =>
        {
            var attempt = new Attempt(db, transaction, cancellationToken);
            var result = await operation(attempt);
            attempt.Flush();
            return result;
        }, TransactionOptions.ForMaxAttempts(5), cancellationToken);

    public sealed class Attempt(FirestoreDb db, Transaction transaction, CancellationToken cancellationToken) : IAuditWriter
    {
        private readonly List<Action> writes = [];
        public Task<DocumentSnapshot> ReadAsync(string path) => transaction.GetSnapshotAsync(db.Document(path), cancellationToken);
        public Task<QuerySnapshot> ReadAsync(Query query) => transaction.GetSnapshotAsync(query, cancellationToken);
        public void Create(string path, Dictionary<string, object?> document) => writes.Add(() => transaction.Create(db.Document(path), document));
        public void Update(string path, Dictionary<string, object?> fields) { RequireMutable(path); writes.Add(() => transaction.Update(db.Document(path), fields)); }
        public void Set(string path, Dictionary<string, object?> document) { RequireMutable(path); writes.Add(() => transaction.Set(db.Document(path), document)); }
        public void Delete(string path) { RequireMutable(path); writes.Add(() => transaction.Delete(db.Document(path))); }
        public void Add(AuditEvent auditEvent) => Create($"audit_events/{auditEvent.Id}", FirestoreDocuments.Audit(auditEvent));
        private static void RequireMutable(string path)
        {
            if (path.StartsWith("audit_events/", StringComparison.Ordinal))
                throw new InvalidOperationException("Audit events are append-only.");
        }
        internal void Flush() { foreach (var write in writes) write(); }
    }
}
