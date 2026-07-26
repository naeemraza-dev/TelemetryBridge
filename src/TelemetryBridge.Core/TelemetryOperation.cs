using System.Diagnostics;

namespace TelemetryBridge.Core;

/// <summary>Measures a custom operation and records a bounded operation type.</summary>
public sealed class TelemetryOperation : IDisposable
{
    private readonly Activity? activity;
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly string operationType;
    private bool disposed;

    private TelemetryOperation(string name, string operationType)
    {
        this.operationType = operationType;
        activity = TelemetryBridgeDiagnostics.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("telemetrybridge.operation.type", operationType);
    }

    /// <summary>Starts a measured operation.</summary>
    public static TelemetryOperation Start(string name, string operationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        return new TelemetryOperation(name, operationType);
    }

    /// <summary>Records an exception without adding exception text to metric dimensions.</summary>
    public void RecordException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
    }

    /// <summary>Adds a low-cardinality attribute to the current operation span.</summary>
    public TelemetryOperation SetTag(string key, object? value)
    {
        activity?.SetTag(key, value);
        return this;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        var duration = Stopwatch.GetElapsedTime(started).TotalSeconds;
        var tags = new TagList { { "telemetrybridge.operation.type", operationType } };
        TelemetryBridgeDiagnostics.Operations.Add(1, tags);
        TelemetryBridgeDiagnostics.OperationDuration.Record(duration, tags);
        activity?.Dispose();
    }
}
