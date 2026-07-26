using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TelemetryBridge.Core;

/// <summary>Shared, vendor-neutral diagnostics primitives used by instrumented applications.</summary>
public static class TelemetryBridgeDiagnostics
{
    /// <summary>The activity source name registered by TelemetryBridge.</summary>
    public const string ActivitySourceName = "TelemetryBridge";

    /// <summary>The meter name registered by TelemetryBridge.</summary>
    public const string MeterName = "TelemetryBridge";

    /// <summary>Creates custom application activities.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Creates custom application metrics.</summary>
    public static readonly Meter Meter = new(MeterName);

    /// <summary>Total number of controlled application operations.</summary>
    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "telemetrybridge.operation.count",
        description: "Number of controlled application operations.");

    /// <summary>Duration of controlled application operations.</summary>
    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "telemetrybridge.operation.duration",
        unit: "s",
        description: "Duration of controlled application operations.");

    /// <summary>Number of durable work items observed as pending during a worker poll.</summary>
    public static readonly Histogram<long> WorkItemsPending = Meter.CreateHistogram<long>(
        "telemetrybridge.work_items.pending",
        unit: "{item}",
        description: "Pending durable work items observed during a worker poll.");

    /// <summary>Total number of durable work items successfully processed.</summary>
    public static readonly Counter<long> WorkItemsProcessed = Meter.CreateCounter<long>(
        "telemetrybridge.work_items.processed",
        unit: "{item}",
        description: "Durable work items successfully processed.");

    /// <summary>Number of safe shadow-validation requests by bounded outcome.</summary>
    public static readonly Counter<long> ShadowValidations = Meter.CreateCounter<long>(
        "telemetrybridge.shadow.validations",
        unit: "{request}",
        description: "Safe, read-only modernization shadow validations.");

    /// <summary>Duration of Strangler routing decisions.</summary>
    public static readonly Histogram<double> RouteDecisionDuration = Meter.CreateHistogram<double>(
        "telemetrybridge.route.decision.duration",
        unit: "s",
        description: "Time required to load migration state and select a backend.");

    /// <summary>Number of requests routed to each bounded modernization target.</summary>
    public static readonly Counter<long> RoutedRequests = Meter.CreateCounter<long>(
        "telemetrybridge.route.requests",
        unit: "{request}",
        description: "Requests routed by modernization target.");
}
