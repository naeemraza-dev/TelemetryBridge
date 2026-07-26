using OpenTelemetry.Trace;
using TelemetryBridge.AspNetCore;

namespace TelemetryBridge.UnitTests;

public sealed class SamplingTests
{
    [Fact]
    public void CreateSamplerAlwaysOffReturnsAlwaysOffSampler()
    {
        var sampler = TelemetryBridgeExtensions.CreateSampler(new TracingOptions
        {
            SamplingMode = "AlwaysOff"
        });

        Assert.IsType<AlwaysOffSampler>(sampler);
    }

    [Fact]
    public void ValidatorRejectsInvalidRatio()
    {
        var options = new TelemetryBridgeOptions
        {
            ServiceName = "orders"
        };
        options.Tracing.SamplingRatio = 1.5;

        var result = new TelemetryBridgeOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
    }
}
