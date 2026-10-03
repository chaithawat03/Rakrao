using Microsoft.EntityFrameworkCore;
using Npgsql;
using RakRao.Domain.Audit;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

[Collection("PostgreSQL")]
public class AuditDatabaseGuardTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Direct_sql_cannot_update_delete_or_truncate_audit_events()
    {
        var connectionString = Environment.GetEnvironmentVariable("RAKRAO_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<RakRaoDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RakRaoDbContext(options);
        await db.Database.MigrateAsync();

        var audit = new AuditEvent(null, "TEST", "USER", Guid.NewGuid(),
            "audit-guard-check", DateTimeOffset.UtcNow);
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();

        var updateError = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE audit_events SET action = {"MUTATED"} WHERE id = {audit.Id}"));
        var deleteError = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM audit_events WHERE id = {audit.Id}"));
        var truncateError = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE audit_events"));

        Assert.Equal("P0001", updateError.SqlState);
        Assert.Equal("P0001", deleteError.SqlState);
        Assert.Equal("P0001", truncateError.SqlState);
        Assert.Equal("TEST", await db.AuditEvents.AsNoTracking()
            .Where(x => x.Id == audit.Id).Select(x => x.Action).SingleAsync());
    }
}
