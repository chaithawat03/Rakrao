using Microsoft.EntityFrameworkCore;
using RakRao.Application;
using RakRao.Infrastructure;
using RakRao.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.MapHealthChecks("/health");
app.MapGet("/health/ready", async (RakRaoDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Run();

public partial class Program;
