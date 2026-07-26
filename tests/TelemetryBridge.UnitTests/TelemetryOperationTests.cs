using System.Diagnostics;
using System.Diagnostics.Metrics;
using TelemetryBridge.Core;

namespace TelemetryBridge.UnitTests;

public sealed class TelemetryOperationTests
{
    [Fact]
    public void StartCreatesControlledActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryBridgeDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(listener);

        using (TelemetryOperation.Start("order.create", "create"))
        {
        }

        Assert.NotNull(stopped);
        Assert.Equal("order.create", stopped.OperationName);
        Assert.Equal("create", stopped.GetTagItem("telemetrybridge.operation.type"));
    }

    [Fact]
    public void DisposeRecordsCounterAndDuration()
    {
        var measurements = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TelemetryBridgeDiagnostics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
            measurements.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            measurements.Add(instrument.Name));
        listener.Start();

        using (TelemetryOperation.Start("order.create", "create"))
        {
        }

        Assert.Contains("telemetrybridge.operation.count", measurements);
        Assert.Contains("telemetrybridge.operation.duration", measurements);
    }
}
