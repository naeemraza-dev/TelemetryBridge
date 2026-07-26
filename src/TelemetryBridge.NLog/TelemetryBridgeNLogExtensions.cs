using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace TelemetryBridge.NLog;

/// <summary>NLog integration extensions that preserve the application's existing targets.</summary>
public static class TelemetryBridgeNLogExtensions
{
    /// <summary>
    /// Adds NLog as a Microsoft logging provider. Do not also configure a direct NLog OTLP target
    /// for the same logger categories, otherwise records will be exported twice.
    /// </summary>
    public static ILoggingBuilder AddTelemetryBridgeNLog(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddNLog();
        return builder;
    }

    /// <summary>Adds NLog scope properties for direct NLog calls made during an HTTP request.</summary>
    public static IApplicationBuilder UseTelemetryBridgeNLogCorrelation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TelemetryBridgeNLogMiddleware>();
    }
}
