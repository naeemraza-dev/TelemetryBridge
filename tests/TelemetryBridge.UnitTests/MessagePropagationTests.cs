using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using TelemetryBridge.Core;

namespace TelemetryBridge.UnitTests;

public sealed class MessagePropagationTests
{
    [Fact]
    public void CapturedMessageContinuesTheSameTrace()
    {
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
            [new TraceContextPropagator(), new BaggagePropagator()]));
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var producer = TelemetryBridgeDiagnostics.ActivitySource.StartActivity(
            "message produce",
            ActivityKind.Producer);
        Assert.NotNull(producer);

        var envelope = TelemetryMessageContext.Capture();
        using var consumer = envelope.StartConsumerActivity("message process");

        Assert.NotNull(consumer);
        Assert.Equal(producer.TraceId, consumer.TraceId);
        Assert.Equal(producer.SpanId, consumer.ParentSpanId);
    }

    [Fact]
    public void BaggageIsDeniedUnlessExplicitlyAllowed()
    {
        OpenTelemetry.Baggage.SetBaggage("tenant.id", "untrusted");
        OpenTelemetry.Baggage.SetBaggage("workflow", "approved");

        var denied = TelemetryMessageContext.Capture();
        var allowed = TelemetryMessageContext.Capture(new HashSet<string>(["workflow"]));

        Assert.Null(denied.Baggage);
        Assert.DoesNotContain("tenant.id", allowed.Baggage);
        Assert.Contains("workflow=approved", allowed.Baggage);
        OpenTelemetry.Baggage.Current = default;
    }

    [Fact]
    public void FanInCreatesLinksForEveryValidMessageContext()
    {
        var first = new Activity("first").Start();
        var firstContext = TelemetryMessageContext.Capture();
        first.Stop();
        var second = new Activity("second").Start();
        var secondContext = TelemetryMessageContext.Capture();
        second.Stop();

        var links = TelemetryMessageContext.CreateLinks([firstContext, secondContext]).ToList();

        Assert.Equal(2, links.Count);
        Assert.Equal(first.TraceId, links[0].Context.TraceId);
        Assert.Equal(second.TraceId, links[1].Context.TraceId);
    }
}
