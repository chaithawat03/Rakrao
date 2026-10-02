using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RakRao.Api;
using RakRao.Application;
using RakRao.Application.Identity;
using RakRao.Infrastructure;
using RakRao.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(FirebaseAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, FirebaseAuthenticationHandler>(FirebaseAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var requestId = RequestId.Resolve(context.Request.Headers["X-Request-ID"].ToString());
    context.TraceIdentifier = requestId;
    context.Response.Headers["X-Request-ID"] = requestId;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
    {
        await next(context);
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapGet("/health/ready", async (RakRaoDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapGet("/api/v1/me", async (HttpContext context, IMeService me, CancellationToken cancellationToken) =>
{
    var uid = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var user = await me.GetAsync(uid, cancellationToken);
    return user is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "User not provisioned",
            detail: "Provision this authenticated account first.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "USER_NOT_PROVISIONED", ["requestId"] = context.TraceIdentifier
            })
        : Results.Ok(user);
}).RequireAuthorization();

app.MapPost("/api/v1/me", async (HttpContext context, IMeService me, CancellationToken cancellationToken) =>
{
    var uid = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var displayName = context.User.FindFirstValue(ClaimTypes.Name);
    var user = await me.ProvisionAsync(new VerifiedFirebaseUser(uid, displayName), context.TraceIdentifier,
        cancellationToken);
    return Results.Ok(user);
}).RequireAuthorization();

app.Run();

public partial class Program;
