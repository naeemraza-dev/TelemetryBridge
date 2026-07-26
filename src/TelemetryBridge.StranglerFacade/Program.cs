using System.Diagnostics;
using System.Net;
using TelemetryBridge.AspNetCore;
using TelemetryBridge.Core;
using TelemetryBridge.StranglerFacade;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTelemetryBridge(builder.Configuration, options =>
{
    options.ServiceName = "telemetrybridge-strangler-facade";
    options.ServiceVersion = "0.1.0";
    options.Environment = builder.Environment.EnvironmentName;
});
builder.Services.AddHttpForwarder();
builder.Services.AddSingleton(new MigrationConfigurationStore(
    builder.Configuration["Migration:FilePath"] ?? "data/migration.json"));
builder.Services.AddSingleton<BackendHealthState>();
builder.Services.AddHostedService<BackendHealthMonitor>();
builder.Services.AddSingleton<ShadowValidationDispatcher>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ShadowValidationDispatcher>());
builder.Services.AddSingleton(new BackendAddresses(
    new Uri(builder.Configuration["Backends:Modern"] ?? "http://localhost:8081/"),
    new Uri(builder.Configuration["Backends:Legacy"] ?? "http://localhost:8083/")));
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseTelemetryBridge();
app.MapHealthChecks("/health");
app.MapGet("/openapi/public-api.yaml", () =>
    Results.File(
        Path.Combine(app.Environment.ContentRootPath, "public-api.yaml"),
        "application/yaml"));

var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
var configurationStore = app.Services.GetRequiredService<MigrationConfigurationStore>();
var health = app.Services.GetRequiredService<BackendHealthState>();
var addresses = app.Services.GetRequiredService<BackendAddresses>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var shadowDispatcher = app.Services.GetRequiredService<ShadowValidationDispatcher>();
var invoker = new HttpMessageInvoker(new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
    UseProxy = false,
    ActivityHeadersPropagator = DistributedContextPropagator.Current
});
var requestConfig = new ForwarderRequestConfig
{
    ActivityTimeout = TimeSpan.FromSeconds(30)
};

app.MapFallback(async context =>
{
    var decisionStarted = Stopwatch.GetTimestamp();
    var migration = await configurationStore.ReadAsync(context.RequestAborted);
    if (migration.Mode == "shadow"
        && HttpMethods.IsGet(context.Request.Method)
        && context.Request.Path.StartsWithSegments("/api/payments"))
    {
        var shadowAddress = new Uri(
            addresses.Modern,
            context.Request.Path + context.Request.QueryString);
        if (!shadowDispatcher.TryEnqueue(shadowAddress, TelemetryMessageContext.Capture()))
        {
            TelemetryBridgeDiagnostics.ShadowValidations.Add(
                1,
                new TagList { { "outcome", "queue_full" } });
        }
    }
    var target = RouteDecision.Select(context, migration, app.Environment);
    TelemetryBridgeDiagnostics.RouteDecisionDuration.Record(
        Stopwatch.GetElapsedTime(decisionStarted).TotalSeconds,
        new TagList
        {
            { "telemetrybridge.modernization.target", target.ToString().ToLowerInvariant() }
        });
    if (!health.IsHealthy(target))
    {
        var fallback = target == ModernizationTarget.Modern
            ? ModernizationTarget.Legacy
            : ModernizationTarget.Modern;
        if (RouteDecision.SupportsBoth(context.Request.Path) && health.IsHealthy(fallback))
        {
            Activity.Current?.AddEvent(new ActivityEvent("telemetrybridge.modernization.fallback"));
            FacadeLogs.Fallback(logger, target.ToString(), fallback.ToString());
            target = fallback;
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(
                new { error = "The requested operation is temporarily unavailable." },
                context.RequestAborted);
            return;
        }
    }

    var targetName = target.ToString().ToLowerInvariant();
    TelemetryBridgeDiagnostics.RoutedRequests.Add(
        1,
        new TagList { { "telemetrybridge.modernization.target", targetName } });
    using var routeActivity = TelemetryBridgeDiagnostics.ActivitySource.StartActivity(
        "strangler.route",
        ActivityKind.Internal);
    routeActivity?.SetTag("telemetrybridge.modernization.target", targetName);
    routeActivity?.SetTag("http.route", RouteDecision.Normalize(context.Request.Path));
    var destination = target == ModernizationTarget.Modern ? addresses.Modern : addresses.Legacy;
    var error = await forwarder.SendAsync(
        context,
        destination.AbsoluteUri,
        invoker,
        requestConfig,
        HttpTransformer.Default);
    if (error != ForwarderError.None)
    {
        var errorFeature = context.Features.Get<IForwarderErrorFeature>();
        routeActivity?.SetStatus(ActivityStatusCode.Error, error.ToString());
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
        }
        FacadeLogs.ProxyFailure(logger, error.ToString(), errorFeature?.Exception);
    }
});

await app.RunAsync();
public partial class Program;
