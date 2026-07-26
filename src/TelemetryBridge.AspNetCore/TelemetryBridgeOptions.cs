using System.ComponentModel.DataAnnotations;

namespace TelemetryBridge.AspNetCore;

/// <summary>Configuration for all TelemetryBridge signals.</summary>
public sealed class TelemetryBridgeOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TelemetryBridge";

    /// <summary>Gets or sets whether telemetry is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the stable service name.</summary>
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the service namespace.</summary>
    public string ServiceNamespace { get; set; } = "telemetrybridge";

    /// <summary>Gets or sets the deployed version.</summary>
    public string ServiceVersion { get; set; } = "0.0.0";

    /// <summary>Gets or sets the deployment environment.</summary>
    public string Environment { get; set; } = "Development";

    /// <summary>Gets or sets the cloud provider, when known.</summary>
    public string? CloudProvider { get; set; }

    /// <summary>Gets or sets the cloud region, when known.</summary>
    public string? CloudRegion { get; set; }

    /// <summary>Gets OTLP export options.</summary>
    public OtlpOptions Otlp { get; init; } = new();

    /// <summary>Gets trace options.</summary>
    public TracingOptions Tracing { get; init; } = new();

    /// <summary>Gets metric options.</summary>
    public SignalOptions Metrics { get; init; } = new();

    /// <summary>Gets log options.</summary>
    public LoggingOptions Logging { get; init; } = new();

    /// <summary>Gets database-instrumentation safety options.</summary>
    public DatabaseTelemetryOptions Database { get; init; } = new();
}

/// <summary>Database telemetry options. Query parameters are never enabled by this package.</summary>
public sealed class DatabaseTelemetryOptions
{
    /// <summary>
    /// Gets or sets whether parameterized command text may be emitted in the Development environment.
    /// This must remain false outside isolated development.
    /// </summary>
    public bool CaptureParameterizedTextInDevelopment { get; set; }
}

/// <summary>OTLP exporter configuration.</summary>
public sealed class OtlpOptions
{
    /// <summary>Gets or sets the collector endpoint.</summary>
    public Uri Endpoint { get; set; } = new("http://localhost:4317");

    /// <summary>Gets or sets Grpc or HttpProtobuf.</summary>
    public string Protocol { get; set; } = "Grpc";
}

/// <summary>Tracing and head-sampling configuration.</summary>
public sealed class TracingOptions
{
    /// <summary>Gets or sets whether tracing is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets AlwaysOn, AlwaysOff, TraceIdRatio, ParentBasedAlwaysOn, or ParentBasedTraceIdRatio.</summary>
    public string SamplingMode { get; set; } = "ParentBasedTraceIdRatio";

    /// <summary>Gets or sets the ratio used by ratio samplers.</summary>
    [Range(0, 1)]
    public double SamplingRatio { get; set; } = 1;
}

/// <summary>Basic signal configuration.</summary>
public class SignalOptions
{
    /// <summary>Gets or sets whether the signal is enabled.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>OpenTelemetry log configuration.</summary>
public sealed class LoggingOptions : SignalOptions
{
    /// <summary>Gets or sets whether formatted messages are exported.</summary>
    public bool IncludeFormattedMessage { get; set; } = true;

    /// <summary>Gets or sets whether logging scopes are exported.</summary>
    public bool IncludeScopes { get; set; } = true;
}
