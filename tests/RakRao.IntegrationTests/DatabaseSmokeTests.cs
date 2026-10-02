using Microsoft.EntityFrameworkCore;
using RakRao.Infrastructure.Persistence;

namespace RakRao.IntegrationTests;

public class DatabaseSmokeTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Initial_migration_applies_to_test_database()
    {
        var connectionString = Environment.GetEnvironmentVariable("RAKRAO_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Set RAKRAO_TEST_CONNECTION_STRING to the separate integration-test database.");

        var options = new DbContextOptionsBuilder<RakRaoDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new RakRaoDbContext(options);

        await db.Database.MigrateAsync();
        Assert.True(await db.Database.CanConnectAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
