using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelemetryBridge.AspNetCore;

internal sealed class TelemetryBridgeMiddleware(
    RequestDelegate next,
    ILogger<TelemetryBridgeMiddleware> logger,
    IOptions<TelemetryBridgeOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var supplied)
            && supplied.Count > 0
            && supplied[0] is { Length: > 0 and <= 128 } valid
                ? valid
                : context.TraceIdentifier;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = activity?.TraceId.ToHexString(),
            ["SpanId"] = activity?.SpanId.ToHexString(),
            ["RequestId"] = context.TraceIdentifier,
            ["CorrelationId"] = correlationId,
            ["service.name"] = options.Value.ServiceName,
            ["deployment.environment.name"] = options.Value.Environment
        });

        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next(context).ConfigureAwait(false);
    }
}
