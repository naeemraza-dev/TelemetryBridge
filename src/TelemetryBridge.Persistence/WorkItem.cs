namespace TelemetryBridge.Persistence;

/// <summary>A durable, non-sensitive sample message with serialized W3C propagation context.</summary>
public sealed class WorkItem
{
    public Guid Id { get; set; }

    public string Operation { get; set; } = "order.created";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }

    public string? Baggage { get; set; }
}
