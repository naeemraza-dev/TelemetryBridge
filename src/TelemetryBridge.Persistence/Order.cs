namespace TelemetryBridge.Persistence;

/// <summary>A deliberately non-sensitive sample order record.</summary>
public sealed class Order
{
    /// <summary>Gets or sets the database identifier. It is never emitted as telemetry.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the controlled sales channel.</summary>
    public string Channel { get; set; } = "web";

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
