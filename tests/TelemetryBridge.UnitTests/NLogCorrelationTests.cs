using System.Diagnostics;
using NLog;
using TelemetryBridge.NLog;

namespace TelemetryBridge.UnitTests;

public sealed class NLogCorrelationTests
{
    [Fact]
    public void RendererUsesActiveActivityIdentifiers()
    {
        using var activity = new Activity("test").Start();
        var traceRenderer = new TelemetryBridgeCorrelationLayoutRenderer { Item = "TraceId" };
        var spanRenderer = new TelemetryBridgeCorrelationLayoutRenderer { Item = "SpanId" };
        var logEvent = new LogEventInfo(LogLevel.Info, "test", "message");

        Assert.Equal(activity.TraceId.ToHexString(), traceRenderer.Render(logEvent));
        Assert.Equal(activity.SpanId.ToHexString(), spanRenderer.Render(logEvent));
    }
}
