using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RakRao.Infrastructure.Identity;

namespace RakRao.IntegrationTests;

public class FirebaseVerifierTests
{
    [Fact]
    public async Task Official_sdk_verifies_emulator_token_and_rejects_expiry_or_wrong_project()
    {
        var previous = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");
        Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", "127.0.0.1:9099");
        try
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ProjectId"] = "demo-rakrao"
            }).Build();
            using var verifier = new FirebaseIdTokenVerifier(settings, new DevelopmentEnvironment(), TimeProvider.System);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var valid = await verifier.VerifyAsync(Token("demo-rakrao", now - 10, now + 3600), CancellationToken.None);
            var expired = await verifier.VerifyAsync(Token("demo-rakrao", now - 3600, now - 10), CancellationToken.None);
            var wrongProject = await verifier.VerifyAsync(Token("demo-other", now - 10, now + 3600), CancellationToken.None);

            Assert.Equal("emulator-user", valid?.Uid);
            Assert.Null(expired);
            Assert.Null(wrongProject);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", previous);
        }
    }

    private static string Token(string projectId, long issuedAt, long expiresAt)
    {
        static string Encode(object value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Encode(new { alg = "none", typ = "JWT" });
        var payload = Encode(new Dictionary<string, object>
        {
            ["iss"] = $"https://securetoken.google.com/{projectId}",
            ["aud"] = projectId,
            ["sub"] = "emulator-user",
            ["iat"] = issuedAt,
            ["exp"] = expiresAt,
            ["auth_time"] = issuedAt
        });
        return $"{header}.{payload}.";
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RakRao.IntegrationTests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
