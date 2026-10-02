using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RakRao.Application.Identity;

namespace RakRao.Infrastructure.Identity;

public sealed class FirebaseIdTokenVerifier : IFirebaseIdTokenVerifier, IDisposable
{
    private readonly FirebaseApp app;
    private readonly FirebaseAuth auth;
    private readonly string projectId;
    private readonly TimeProvider clock;

    public FirebaseIdTokenVerifier(IConfiguration configuration, IHostEnvironment environment, TimeProvider clock)
    {
        projectId = configuration["Firebase:ProjectId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("Firebase:ProjectId must be configured.");
        this.clock = clock;

        var emulatorHost = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");
        if (!environment.IsDevelopment() && !string.IsNullOrWhiteSpace(emulatorHost))
            throw new InvalidOperationException("Firebase Auth Emulator is allowed only in Development.");

        var credential = string.IsNullOrWhiteSpace(emulatorHost)
            ? GoogleCredential.GetApplicationDefault()
            : GoogleCredential.FromAccessToken("owner");
        app = FirebaseApp.Create(new AppOptions { ProjectId = projectId, Credential = credential },
            $"rakrao-{Guid.NewGuid():N}");
        auth = FirebaseAuth.GetAuth(app);
    }

    public async Task<VerifiedFirebaseUser?> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var verified = await auth.VerifyIdTokenAsync(token, cancellationToken);
            var now = clock.GetUtcNow().ToUnixTimeSeconds();
            if (verified.Audience != projectId ||
                verified.Issuer != $"https://securetoken.google.com/{projectId}" ||
                verified.ExpirationTimeSeconds <= now ||
                verified.IssuedAtTimeSeconds > now ||
                string.IsNullOrWhiteSpace(verified.Subject) ||
                verified.Subject != verified.Uid || verified.Uid.Length > 128)
                return null;
            var displayName = verified.Claims.TryGetValue("name", out var name) ? name as string : null;
            return new VerifiedFirebaseUser(verified.Uid, displayName);
        }
        catch (FirebaseAuthException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public void Dispose() => app.Delete();
}
