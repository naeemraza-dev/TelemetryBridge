using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace TelemetryBridge.Core;

/// <summary>Serializable W3C propagation fields for an application-owned message envelope.</summary>
public sealed record TelemetryMessageContext(string? TraceParent, string? TraceState, string? Baggage)
{
    private static readonly TextMapPropagator Propagator = new CompositeTextMapPropagator(
        [new TraceContextPropagator(), new BaggagePropagator()]);

    /// <summary>Captures the current trace and explicitly allowed baggage for a message.</summary>
    public static TelemetryMessageContext Capture(ISet<string>? allowedBaggageKeys = null)
    {
        var carrier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var currentBaggage = OpenTelemetry.Baggage.Current;
        var baggage = OpenTelemetry.Baggage.Create(
            OpenTelemetry.Baggage.GetBaggage(currentBaggage)
                .Where(pair => allowedBaggageKeys?.Contains(pair.Key) == true)
                .ToDictionary());
        Propagator.Inject(new PropagationContext(Activity.Current?.Context ?? default, baggage), carrier, Setter);
        return new(
            carrier.GetValueOrDefault("traceparent"),
            carrier.GetValueOrDefault("tracestate"),
            carrier.GetValueOrDefault("baggage"));
    }

    /// <summary>Extracts the remote parent and controlled baggage from a message.</summary>
    public PropagationContext Extract(ISet<string>? allowedBaggageKeys = null)
    {
        var carrier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddIfPresent(carrier, "traceparent", TraceParent);
        AddIfPresent(carrier, "tracestate", TraceState);
        AddIfPresent(carrier, "baggage", Baggage);
        var extracted = Propagator.Extract(default, carrier, Getter);
        return new PropagationContext(
            extracted.ActivityContext,
            OpenTelemetry.Baggage.Create(
                OpenTelemetry.Baggage.GetBaggage(extracted.Baggage)
                    .Where(pair => allowedBaggageKeys?.Contains(pair.Key) == true)
                    .ToDictionary()));
    }

    /// <summary>Starts a consumer activity parented to this message context.</summary>
    public Activity? StartConsumerActivity(string name, ISet<string>? allowedBaggageKeys = null)
    {
        var extracted = Extract(allowedBaggageKeys);
        OpenTelemetry.Baggage.Current = extracted.Baggage;
        return TelemetryBridgeDiagnostics.ActivitySource.StartActivity(
            name,
            ActivityKind.Consumer,
            extracted.ActivityContext);
    }

    /// <summary>Creates links for a batch/fan-in activity without selecting one message as its parent.</summary>
    public static IEnumerable<ActivityLink> CreateLinks(IEnumerable<TelemetryMessageContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        return contexts
            .Select(context => context.Extract().ActivityContext)
            .Where(context => context.IsValid())
            .Select(context => new ActivityLink(context));
    }

    private static void AddIfPresent(Dictionary<string, string> carrier, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            carrier[key] = value;
        }
    }

    private static void Setter(Dictionary<string, string> carrier, string key, string value) => carrier[key] = value;

    private static IEnumerable<string> Getter(Dictionary<string, string> carrier, string key) =>
        carrier.TryGetValue(key, out var value) ? [value] : [];
}
