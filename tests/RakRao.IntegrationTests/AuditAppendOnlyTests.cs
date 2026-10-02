using Microsoft.EntityFrameworkCore;
using RakRao.Domain.Audit;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

public class AuditAppendOnlyTests
{
    [Fact]
    public async Task Existing_audit_events_cannot_be_changed_through_either_save_overload()
    {
        var options = new DbContextOptionsBuilder<RakRaoDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused")
            .Options;
        await using var db = new RakRaoDbContext(options);
        var audit = new AuditEvent(null, "TEST", "USER", Guid.NewGuid(), "request-1", DateTimeOffset.UtcNow);
        db.AuditEvents.Attach(audit);
        db.Entry(audit).State = EntityState.Modified;

        var asyncError = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(true, CancellationToken.None));
        var syncError = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Equal("Audit events are append-only.", asyncError.Message);
        Assert.Equal("Audit events are append-only.", syncError.Message);
    }
}
