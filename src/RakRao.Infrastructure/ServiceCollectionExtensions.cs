using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RakRao.Application.Families;
using RakRao.Application.Identity;
using RakRao.Infrastructure.Families;
using RakRao.Infrastructure.Identity;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provider =>
        {
            var projectId = configuration["Firestore:ProjectId"];
            if (string.IsNullOrWhiteSpace(projectId)) throw new InvalidOperationException("Firestore:ProjectId must be configured.");
            var emulator = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
            if (!string.IsNullOrWhiteSpace(emulator) && !provider.GetRequiredService<IHostEnvironment>().IsDevelopment())
                throw new InvalidOperationException("Firestore Emulator is allowed only in Development.");
            return new FirestoreDbBuilder { ProjectId = projectId, DatabaseId = configuration["Firestore:DatabaseId"] ?? "(default)",
                EmulatorDetection = string.IsNullOrWhiteSpace(emulator) ? EmulatorDetection.None : EmulatorDetection.EmulatorOnly }.Build();
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<FirestoreReadiness>();
        services.AddSingleton<IFirestoreUnitOfWork, FirestoreUnitOfWork>();
        services.AddSingleton<IFirebaseIdTokenVerifier, FirebaseIdTokenVerifier>();
        services.AddScoped<IMeService, FirestoreMeService>();
        services.AddScoped<IFamilyService, FirestoreFamilyService>();
        return services;
    }
}
