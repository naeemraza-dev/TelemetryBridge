using TelemetryBridge.AspNetCore;
using TelemetryBridge.Core;
using TelemetryBridge.NLog;

var legacyLogger = NLog.LogManager.GetCurrentClassLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTelemetryBridge(builder.Configuration, options =>
{
    options.ServiceName = "telemetrybridge-legacy-api";
    options.ServiceVersion = "0.1.0";
    options.Environment = builder.Environment.EnvironmentName;
});
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseTelemetryBridge();
app.UseTelemetryBridgeNLogCorrelation();

app.MapGet("/api/customers", () =>
{
    using var operation = TelemetryOperation.Start("customer.list.legacy", "list");
    legacyLogger.Info("Legacy customer list executed");
    return Results.Ok(new[]
    {
        new CustomerResponse("CUST-REDACTED", "active")
    });
});

app.MapMethods("/api/payments/{**path}", ["GET", "POST"], (HttpContext context) =>
{
    using var operation = TelemetryOperation.Start("payment.legacy", context.Request.Method.ToLowerInvariant());
    return Results.Ok(new { implementation = "legacy", status = "accepted" });
});

app.MapHealthChecks("/health");
await app.RunAsync();
NLog.LogManager.Shutdown();

internal sealed record CustomerResponse(string Reference, string Status);
public partial class Program;
