using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NLog;
using TelemetryBridge.AspNetCore;

namespace TelemetryBridge.NLog;

internal sealed class TelemetryBridgeNLogMiddleware(
    RequestDelegate next,
    IOptions<TelemetryBridgeOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;
        using var properties = ScopeContext.PushProperties([
            new KeyValuePair<string, object?>("TraceId", activity?.TraceId.ToHexString() ?? string.Empty),
            new KeyValuePair<string, object?>("SpanId", activity?.SpanId.ToHexString() ?? string.Empty),
            new KeyValuePair<string, object?>("RequestId", context.TraceIdentifier),
            new KeyValuePair<string, object?>("CorrelationId", correlationId),
            new KeyValuePair<string, object?>("service.name", options.Value.ServiceName),
            new KeyValuePair<string, object?>("deployment.environment.name", options.Value.Environment)
        ]);
        await next(context).ConfigureAwait(false);
    }
}
