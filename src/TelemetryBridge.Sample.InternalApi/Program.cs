using TelemetryBridge.AspNetCore;
using TelemetryBridge.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTelemetryBridge(builder.Configuration, options =>
{
    options.ServiceName = "telemetrybridge-internal-api";
    options.ServiceVersion = "0.1.0";
    options.Environment = builder.Environment.EnvironmentName;
});
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseTelemetryBridge();

app.MapPost("/api/inventory/reservations", async (
    ReservationRequest request,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    using var operation = TelemetryOperation.Start("inventory.reserve", "reserve");
    await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
    InternalApiLogs.ReservationAccepted(logger, request.Channel);
    return Results.Ok(new ReservationResponse("accepted"));
});

app.MapHealthChecks("/health");
await app.RunAsync();

internal sealed record ReservationRequest(string Channel);
internal sealed record ReservationResponse(string Status);
public partial class Program;

internal static partial class InternalApiLogs
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Inventory reservation accepted for channel {Channel}")]
    public static partial void ReservationAccepted(ILogger logger, string channel);
}
