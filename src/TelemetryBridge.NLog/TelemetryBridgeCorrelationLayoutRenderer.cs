using System.Diagnostics;
using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace TelemetryBridge.NLog;

/// <summary>Renders active trace correlation and controlled service identity for direct NLog calls.</summary>
[LayoutRenderer("telemetrybridge-correlation")]
public sealed class TelemetryBridgeCorrelationLayoutRenderer : LayoutRenderer
{
    /// <summary>Gets or sets TraceId, SpanId, TraceFlags, ServiceName, or Environment.</summary>
    public string Item { get; set; } = "TraceId";

    /// <inheritdoc />
    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
    {
        var activity = Activity.Current;
        var value = Item.ToUpperInvariant() switch
        {
            "TRACEID" => activity?.TraceId.ToHexString(),
            "SPANID" => activity?.SpanId.ToHexString(),
            "TRACEFLAGS" => activity?.ActivityTraceFlags.ToString(),
            "SERVICENAME" => GetProperty(logEvent, "service.name")
                ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"),
            "ENVIRONMENT" => GetProperty(logEvent, "deployment.environment.name")
                ?? Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT"),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.Append(value);
        }
    }

    private static string? GetProperty(LogEventInfo logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) ? value?.ToString() : null;
}
