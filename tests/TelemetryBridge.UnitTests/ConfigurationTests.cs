using Microsoft.Extensions.Configuration;
using TelemetryBridge.AspNetCore;

namespace TelemetryBridge.UnitTests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ConfigurationBindsAllSignalAndDatabaseOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TelemetryBridge:ServiceName"] = "orders-api",
                ["TelemetryBridge:ServiceNamespace"] = "commerce",
                ["TelemetryBridge:Tracing:SamplingMode"] = "ParentBasedTraceIdRatio",
                ["TelemetryBridge:Tracing:SamplingRatio"] = "0.25",
                ["TelemetryBridge:Metrics:Enabled"] = "false",
                ["TelemetryBridge:Database:CaptureParameterizedTextInDevelopment"] = "true"
            })
            .Build();

        var options = new TelemetryBridgeOptions();
        configuration.GetSection(TelemetryBridgeOptions.SectionName).Bind(options);

        Assert.Equal("orders-api", options.ServiceName);
        Assert.Equal("commerce", options.ServiceNamespace);
        Assert.Equal(0.25, options.Tracing.SamplingRatio);
        Assert.False(options.Metrics.Enabled);
        Assert.True(options.Database.CaptureParameterizedTextInDevelopment);
    }

    [Fact]
    public void DatabaseTextIsRejectedOutsideDevelopment()
    {
        var options = new TelemetryBridgeOptions
        {
            ServiceName = "orders-api",
            Environment = "Production",
            Database = { CaptureParameterizedTextInDevelopment = true }
        };

        var result = new TelemetryBridgeOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, failure => failure.Contains("Development", StringComparison.Ordinal));
    }
}
