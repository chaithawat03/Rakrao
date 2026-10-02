using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RakRao.Application.Identity;

namespace RakRao.Api;

public sealed class FirebaseAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceProvider services)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Firebase";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return AuthenticateResult.NoResult();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Firebase authentication rejected");
            return AuthenticateResult.Fail("Invalid credentials.");
        }
        var token = header[7..].Trim();
        if (string.IsNullOrWhiteSpace(token) || token.Contains(' '))
        {
            Logger.LogWarning("Firebase authentication rejected");
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        var firebaseUser = await services.GetRequiredService<IFirebaseIdTokenVerifier>()
            .VerifyAsync(token, Context.RequestAborted);
        if (firebaseUser is null)
        {
            Logger.LogWarning("Firebase authentication rejected");
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, firebaseUser.Uid) };
        if (firebaseUser.DisplayName is not null) claims.Add(new Claim(ClaimTypes.Name, firebaseUser.DisplayName));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "about:blank",
            title = "Authentication required",
            status = StatusCodes.Status401Unauthorized,
            detail = "A valid Firebase ID token is required.",
            code = "AUTHENTICATION_REQUIRED",
            requestId = Context.TraceIdentifier
        }));
    }
}
