using Microsoft.Extensions.Options;

namespace TelemetryBridge.AspNetCore;

internal sealed class TelemetryBridgeOptionsValidator : IValidateOptions<TelemetryBridgeOptions>
{
    private static readonly HashSet<string> SamplingModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AlwaysOn",
        "AlwaysOff",
        "TraceIdRatio",
        "ParentBasedAlwaysOn",
        "ParentBasedTraceIdRatio"
    };

    public ValidateOptionsResult Validate(string? name, TelemetryBridgeOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            failures.Add("TelemetryBridge:ServiceName is required.");
        }

        if (!SamplingModes.Contains(options.Tracing.SamplingMode))
        {
            failures.Add($"Unsupported sampling mode '{options.Tracing.SamplingMode}'.");
        }

        if (options.Tracing.SamplingRatio is < 0 or > 1)
        {
            failures.Add("TelemetryBridge:Tracing:SamplingRatio must be between 0 and 1.");
        }

        if (!options.Otlp.Endpoint.IsAbsoluteUri)
        {
            failures.Add("TelemetryBridge:Otlp:Endpoint must be an absolute URI.");
        }

        if (options.ServiceName.Length > 128 || options.ServiceNamespace.Length > 128)
        {
            failures.Add("Service identity values cannot exceed 128 characters.");
        }

        if (options.Database.CaptureParameterizedTextInDevelopment
            && !string.Equals(options.Environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Database command text can only be enabled in the Development environment.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
