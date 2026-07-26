using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using TelemetryBridge.Core;

namespace TelemetryBridge.StranglerFacade;

internal enum ModernizationTarget
{
    Legacy,
    Modern
}

internal sealed record BackendAddresses(Uri Modern, Uri Legacy);

internal static class RouteDecision
{
    public static ModernizationTarget Select(
        HttpContext context,
        MigrationConfiguration configuration,
        IHostEnvironment environment)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/orders"))
        {
            return ModernizationTarget.Modern;
        }
        if (path.StartsWithSegments("/api/customers"))
        {
            return ModernizationTarget.Legacy;
        }
        if (!path.StartsWithSegments("/api/payments"))
        {
            return ModernizationTarget.Legacy;
        }

        if (configuration.HeaderRoutingEnabled
            && environment.IsDevelopment()
            && context.Request.Headers.TryGetValue("X-TelemetryBridge-Route", out var forced))
        {
            return string.Equals(forced, "modern", StringComparison.OrdinalIgnoreCase)
                ? ModernizationTarget.Modern
                : ModernizationTarget.Legacy;
        }

        if (context.Request.Headers.TryGetValue("X-Api-Version", out var version))
        {
            if (string.Equals(version, "2", StringComparison.Ordinal))
            {
                return ModernizationTarget.Modern;
            }
            if (string.Equals(version, "1", StringComparison.Ordinal))
            {
                return ModernizationTarget.Legacy;
            }
        }

        return configuration.Mode switch
        {
            "modern" => ModernizationTarget.Modern,
            "rollout" when Bucket() < configuration.PaymentModernPercentage => ModernizationTarget.Modern,
            _ => ModernizationTarget.Legacy
        };
    }

    public static string Normalize(PathString path)
    {
        if (path.StartsWithSegments("/api/orders")) return "/api/orders/{**catch-all}";
        if (path.StartsWithSegments("/api/customers")) return "/api/customers/{**catch-all}";
        if (path.StartsWithSegments("/api/payments")) return "/api/payments/{**catch-all}";
        return "/{**catch-all}";
    }

    public static bool SupportsBoth(PathString path) =>
        path.StartsWithSegments("/api/payments");

    private static int Bucket()
    {
        var traceId = Activity.Current?.TraceId.ToHexString();
        if (traceId is null)
        {
            return Random.Shared.Next(100);
        }
        return (int)(Convert.ToUInt32(traceId[..8], 16) % 100);
    }
}

internal sealed record ShadowRequest(Uri Address, TelemetryMessageContext Context);

internal sealed class ShadowValidationDispatcher(ILogger<ShadowValidationDispatcher> logger)
    : BackgroundService
{
    private readonly Channel<ShadowRequest> requests =
        Channel.CreateBounded<ShadowRequest>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };

    public bool TryEnqueue(Uri address, TelemetryMessageContext context) =>
        requests.Writer.TryWrite(new ShadowRequest(address, context));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in requests.Reader.ReadAllAsync(stoppingToken))
        {
            using var activity = request.Context.StartConsumerActivity("strangler.shadow.validate");
            try
            {
                using var response = await client.GetAsync(request.Address, stoppingToken);
                var outcome = response.IsSuccessStatusCode ? "success" : "backend_error";
                TelemetryBridgeDiagnostics.ShadowValidations.Add(
                    1,
                    new TagList { { "outcome", outcome } });
                activity?.SetTag("telemetrybridge.shadow.outcome", outcome);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                TelemetryBridgeDiagnostics.ShadowValidations.Add(
                    1,
                    new TagList { { "outcome", "transport_error" } });
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                FacadeLogs.ShadowFailure(logger, exception);
            }
        }
    }

    public override void Dispose()
    {
        client.Dispose();
        base.Dispose();
    }
}

internal sealed class BackendHealthState
{
    private volatile bool modern = true;
    private volatile bool legacy = true;

    public bool IsHealthy(ModernizationTarget target) =>
        target == ModernizationTarget.Modern ? modern : legacy;

    public void Set(ModernizationTarget target, bool healthy)
    {
        if (target == ModernizationTarget.Modern) modern = healthy;
        else legacy = healthy;
    }
}

internal sealed class BackendHealthMonitor(
    BackendAddresses addresses,
    BackendHealthState state,
    ILogger<BackendHealthMonitor> logger) : BackgroundService
{
    private readonly HttpClient client = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAsync(ModernizationTarget.Modern, addresses.Modern, stoppingToken);
            await CheckAsync(ModernizationTarget.Legacy, addresses.Legacy, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task CheckAsync(ModernizationTarget target, Uri address, CancellationToken cancellationToken)
    {
        try
        {
            var healthUri = new Uri(address, "health");
            using var response = await client.GetAsync(healthUri, cancellationToken);
            state.Set(target, response.IsSuccessStatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            state.Set(target, false);
            FacadeLogs.HealthFailure(logger, target.ToString(), exception);
        }
    }

    public override void Dispose()
    {
        client.Dispose();
        base.Dispose();
    }
}

internal static partial class FacadeLogs
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Routing fallback from {Target} to {Fallback}")]
    public static partial void Fallback(ILogger logger, string target, string fallback);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Proxy failed with {ForwarderError}")]
    public static partial void ProxyFailure(ILogger logger, string forwarderError, Exception? exception);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning, Message = "Health check failed for {Target}")]
    public static partial void HealthFailure(ILogger logger, string target, Exception exception);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Warning, Message = "Safe modernization shadow request failed")]
    public static partial void ShadowFailure(ILogger logger, Exception exception);
}
