using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RakRao.Application.Audit;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Infrastructure.Audit;
using RakRao.Infrastructure.Families;
using RakRao.Infrastructure.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.");

        services.AddDbContext<RakRaoDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFirebaseIdTokenVerifier, FirebaseIdTokenVerifier>();
        services.AddScoped<IAuditWriter, EfAuditWriter>();
        services.AddScoped<IMeService, EfMeService>();
        services.AddScoped<IFamilyService, EfFamilyService>();
        return services;
    }
}
