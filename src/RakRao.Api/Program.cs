using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RakRao.Api;
using RakRao.Application;
using RakRao.Application.Families;
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

app.MapGet("/api/v1/families", async (HttpContext context, IFamilyService families,
    CancellationToken cancellationToken) =>
    Results.Ok(await families.ListAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)))
    .RequireAuthorization();

app.MapGet("/api/v1/families/{familyId:guid}", async (Guid familyId, HttpContext context,
    IFamilyService families, CancellationToken cancellationToken) =>
{
    var family = await families.GetAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, familyId,
        cancellationToken);
    if (family is null) return Results.NotFound();
    context.Response.Headers.ETag = $"\"{family.Version}\"";
    return Results.Ok(family);
}).RequireAuthorization();

app.MapPatch("/api/v1/families/{familyId:guid}", async (Guid familyId, UpdateFamilyRequest request,
    HttpContext context, IFamilyService families, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 ||
        request.Description?.Length > 2000)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["A family name of 1 to 160 characters is required."],
            ["description"] = ["Description must be 2000 characters or fewer."]
        });
    var ifMatch = context.Request.Headers.IfMatch.ToString();
    if (ifMatch.Length < 3 || ifMatch[0] != '"' || ifMatch[^1] != '"' ||
        !int.TryParse(ifMatch[1..^1], out var expectedVersion) || expectedVersion < 1)
        return Results.StatusCode(StatusCodes.Status428PreconditionRequired);
    var result = await families.UpdateAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, familyId,
        expectedVersion, request, context.TraceIdentifier, cancellationToken);
    if (result.Family is not null)
        context.Response.Headers.ETag = $"\"{result.Family.Version}\"";
    return result.Outcome switch
    {
        FamilyUpdateOutcome.Updated => Results.Ok(result.Family),
        FamilyUpdateOutcome.Forbidden => Results.Forbid(),
        FamilyUpdateOutcome.Conflict => Results.Conflict(new { code = "STALE_FAMILY_VERSION" }),
        _ => Results.NotFound()
    };
}).RequireAuthorization();

app.MapPost("/api/v1/families", async (CreateFamilyRequest request, HttpContext context,
    IFamilyService families, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 ||
        request.Description?.Length > 2000)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["A family name of 1 to 160 characters is required."],
            ["description"] = ["Description must be 2000 characters or fewer."]
        });
    var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
    if (idempotencyKey.Length > 0 && (idempotencyKey.Length < 8 || idempotencyKey.Length > 128 ||
        idempotencyKey.Any(char.IsControl)))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Idempotency-Key"] = ["Use a key of 8 to 128 printable characters."]
        });
    var result = await families.CreateAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, request,
        idempotencyKey.Length == 0 ? null : idempotencyKey, context.TraceIdentifier, cancellationToken);
    if (result.Family is not null)
        context.Response.Headers.ETag = $"\"{result.Family.Version}\"";
    return result.Outcome switch
    {
        FamilyCreateOutcome.Created => Results.Created($"/api/v1/families/{result.Family!.Id}", result.Family),
        FamilyCreateOutcome.Throttled => Results.StatusCode(StatusCodes.Status429TooManyRequests),
        FamilyCreateOutcome.Conflict => Results.Conflict(new { code = "IDEMPOTENCY_KEY_REUSED" }),
        _ => Results.NotFound()
    };
}).RequireAuthorization();

app.MapPatch("/api/v1/families/{familyId:guid}/members/{memberId:guid}/roles", async (
    Guid familyId, Guid memberId, ChangeFamilyRoleRequest request, HttpContext context,
    IFamilyService families, CancellationToken cancellationToken) =>
{
    if (request.Role is not ("FAMILY_MEMBER" or "FAMILY_ADMIN" or "CREATOR"))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["role"] = ["Role must be FAMILY_MEMBER, FAMILY_ADMIN, or CREATOR."]
        });
    if (request.Grant is null)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["grant"] = ["Specify true to grant or false to revoke the role."]
        });
    var result = await families.ChangeRoleAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
        familyId, memberId, request, context.TraceIdentifier, cancellationToken);
    return result switch
    {
        RoleChangeOutcome.Changed => Results.Ok(),
        RoleChangeOutcome.Forbidden => Results.Forbid(),
        RoleChangeOutcome.Conflict => Results.Conflict(new { code = "LAST_CREATOR" }),
        _ => Results.NotFound()
    };
}).RequireAuthorization();

app.Run();

public partial class Program;
