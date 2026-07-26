using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using TelemetryBridge.AdminApi;
using TelemetryBridge.AspNetCore;
using TelemetryBridge.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTelemetryBridge(builder.Configuration, options =>
{
    options.ServiceName = "telemetrybridge-admin-api";
    options.ServiceVersion = "0.1.0";
    options.Environment = builder.Environment.EnvironmentName;
});
builder.Services.AddSingleton(new MigrationConfigurationStore(
    builder.Configuration["Migration:FilePath"] ?? "data/migration.json"));
builder.Services
    .AddAuthentication(AdminKeyAuthenticationHandler.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, AdminKeyAuthenticationHandler>(
        AdminKeyAuthenticationHandler.AuthenticationScheme,
        _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Operator", policy => policy.RequireRole("Operator", "Admin"))
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseTelemetryBridge();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");

var configuration = app.MapGroup("/api/configuration").RequireAuthorization("Operator");
configuration.MapGet("/migration", async (
    MigrationConfigurationStore store,
    HttpResponse response,
    CancellationToken cancellationToken) =>
{
    var current = await store.ReadAsync(cancellationToken);
    response.Headers.ETag = $"\"{current.Version}\"";
    return Results.Ok(current);
});

configuration.MapPut("/migration", async (
    MigrationUpdateRequest request,
    HttpRequest httpRequest,
    HttpResponse httpResponse,
    ClaimsPrincipal principal,
    MigrationConfigurationStore store,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!TryReadVersion(httpRequest.Headers.IfMatch, out var expectedVersion))
    {
        return Results.BadRequest(new { error = "A numeric If-Match ETag is required." });
    }

    var actor = principal.Identity?.Name ?? "unknown";
    var updated = await store.UpdateAsync(
        expectedVersion,
        request.Mode,
        request.PaymentModernPercentage,
        request.HeaderRoutingEnabled,
        actor,
        cancellationToken);
    if (updated is null)
    {
        return Results.Conflict(new { error = "Configuration version changed; reload and retry." });
    }
    httpResponse.Headers.ETag = $"\"{updated.Version}\"";
    AdminApiLogs.ConfigurationChanged(logger, updated.Version, actor);
    return Results.Ok(updated);
});

configuration.MapGet("/history", async (
    MigrationConfigurationStore store,
    CancellationToken cancellationToken) =>
    Results.Ok(await store.HistoryAsync(cancellationToken)));

configuration.MapPost("/rollback/{version:long}", async (
    long version,
    HttpRequest httpRequest,
    HttpResponse httpResponse,
    ClaimsPrincipal principal,
    MigrationConfigurationStore store,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!TryReadVersion(httpRequest.Headers.IfMatch, out var expectedVersion))
    {
        return Results.BadRequest(new { error = "A numeric If-Match ETag is required." });
    }

    try
    {
        var actor = principal.Identity?.Name ?? "unknown";
        var restored = await store.RollbackAsync(expectedVersion, version, actor, cancellationToken);
        if (restored is null)
        {
            return Results.Conflict(new { error = "Configuration version changed; reload and retry." });
        }
        httpResponse.Headers.ETag = $"\"{restored.Version}\"";
        AdminApiLogs.ConfigurationChanged(logger, restored.Version, actor);
        return Results.Ok(restored);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
}).RequireAuthorization("Admin");

await app.RunAsync();

static bool TryReadVersion(string? etag, out long version) =>
    long.TryParse(etag?.Trim().Trim('"'), out version);

internal sealed record MigrationUpdateRequest(
    string Mode,
    int PaymentModernPercentage,
    bool HeaderRoutingEnabled);
public partial class Program;

internal static partial class AdminApiLogs
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "Migration configuration changed to version {Version} by {Actor}")]
    public static partial void ConfigurationChanged(ILogger logger, long version, string actor);
}
