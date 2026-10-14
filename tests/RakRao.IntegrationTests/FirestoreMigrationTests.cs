using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RakRao.Application.Identity;
using RakRao.Infrastructure;

namespace RakRao.IntegrationTests;

public class FirestoreMigrationTests
{
    [Fact]
    public void Runtime_registers_firestore_without_a_postgres_connection_string()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "demo-rakrao"
        }).Build());
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMeService) &&
            descriptor.ImplementationType?.Name == "FirestoreMeService");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name == "RakRaoDbContext");
    }
}
