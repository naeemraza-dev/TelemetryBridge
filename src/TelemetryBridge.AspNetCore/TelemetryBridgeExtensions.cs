using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelemetryBridge.Core;

namespace TelemetryBridge.AspNetCore;

/// <summary>Registration and middleware extensions for TelemetryBridge.</summary>
public static partial class TelemetryBridgeExtensions
{
    /// <summary>Adds vendor-neutral OpenTelemetry traces, metrics, and logs.</summary>
    public static IServiceCollection AddTelemetryBridge(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TelemetryBridgeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var optionsBuilder = services
            .AddOptions<TelemetryBridgeOptions>()
            .Bind(configuration.GetSection(TelemetryBridgeOptions.SectionName))
            .PostConfigure(ApplyOpenTelemetryEnvironment)
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.AddSingleton<IValidateOptions<TelemetryBridgeOptions>, TelemetryBridgeOptionsValidator>();
        services.AddSingleton<TelemetryAttributePolicy>();

        var effective = new TelemetryBridgeOptions();
        configuration.GetSection(TelemetryBridgeOptions.SectionName).Bind(effective);
        configure?.Invoke(effective);
        ApplyOpenTelemetryEnvironment(effective);

        if (!effective.Enabled)
        {
            return services;
        }

        var resource = BuildResource(effective);
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
        var openTelemetry = services.AddOpenTelemetry();

        if (effective.Tracing.Enabled)
        {
            openTelemetry.WithTracing(tracing => tracing
                .SetResourceBuilder(resource)
                .SetSampler(CreateSampler(effective.Tracing))
                .AddSource(TelemetryBridgeDiagnostics.ActivitySourceName)
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.FilterHttpRequestMessage = request =>
                        request.RequestUri?.AbsolutePath is not "/health";
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    var captureText = effective.Database.CaptureParameterizedTextInDevelopment
                        && effective.Environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
                    options.EnrichWithIDbCommand = (activity, command) =>
                    {
                        activity.SetTag("db.query.text", captureText
                            ? SanitizeSql(command.CommandText)
                            : null);
                        activity.SetTag("db.statement", null);
                    };
                })
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, effective.Otlp)));
        }

        if (effective.Metrics.Enabled)
        {
            openTelemetry.WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddMeter(TelemetryBridgeDiagnostics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, effective.Otlp)));
        }

        if (effective.Logging.Enabled)
        {
            services.AddLogging(logging => logging.AddOpenTelemetry(logs =>
            {
                logs.SetResourceBuilder(resource);
                logs.IncludeFormattedMessage = effective.Logging.IncludeFormattedMessage;
                logs.IncludeScopes = effective.Logging.IncludeScopes;
                logs.ParseStateValues = true;
                logs.AddOtlpExporter(exporter => ConfigureExporter(exporter, effective.Otlp));
            }));
        }

        return services;
    }

    /// <summary>Adds correlation scopes and safe response metadata to the request pipeline.</summary>
    public static IApplicationBuilder UseTelemetryBridge(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TelemetryBridgeMiddleware>();
    }

    internal static Sampler CreateSampler(TracingOptions options) =>
        options.SamplingMode.ToUpperInvariant() switch
        {
            "ALWAYSON" => new AlwaysOnSampler(),
            "ALWAYSOFF" => new AlwaysOffSampler(),
            "TRACEIDRATIO" => new TraceIdRatioBasedSampler(options.SamplingRatio),
            "PARENTBASEDALWAYSON" => new ParentBasedSampler(new AlwaysOnSampler()),
            "PARENTBASEDTRACEIDRATIO" => new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)),
            _ => throw new InvalidOperationException($"Unsupported sampler '{options.SamplingMode}'.")
        };

    private static ResourceBuilder BuildResource(TelemetryBridgeOptions options)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            new("deployment.environment.name", options.Environment),
            new("host.name", System.Environment.MachineName),
            new("service.instance.id", System.Environment.GetEnvironmentVariable("OTEL_SERVICE_INSTANCE_ID")
                ?? System.Environment.MachineName)
        };

        AddIfPresent(attributes, "cloud.provider", options.CloudProvider);
        AddIfPresent(attributes, "cloud.region", options.CloudRegion);
        AddIfPresent(attributes, "container.id", System.Environment.GetEnvironmentVariable("CONTAINER_ID"));
        AddIfPresent(attributes, "k8s.namespace.name", System.Environment.GetEnvironmentVariable("K8S_NAMESPACE_NAME"));
        AddIfPresent(attributes, "k8s.pod.name", System.Environment.GetEnvironmentVariable("K8S_POD_NAME"));
        AddResourceAttributes(attributes, System.Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES"));

        return ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName, options.ServiceNamespace, options.ServiceVersion)
            .AddAttributes(attributes);
    }

    private static void ApplyOpenTelemetryEnvironment(TelemetryBridgeOptions options)
    {
        options.ServiceName = Get("OTEL_SERVICE_NAME") ?? options.ServiceName;
        options.ServiceNamespace = Get("OTEL_SERVICE_NAMESPACE") ?? options.ServiceNamespace;
        options.ServiceVersion = Get("OTEL_SERVICE_VERSION") ?? options.ServiceVersion;
        options.Environment = Get("DEPLOYMENT_ENVIRONMENT") ?? options.Environment;

        if (Uri.TryCreate(Get("OTEL_EXPORTER_OTLP_ENDPOINT"), UriKind.Absolute, out var endpoint))
        {
            options.Otlp.Endpoint = endpoint;
        }

        options.Otlp.Protocol = Get("OTEL_EXPORTER_OTLP_PROTOCOL") ?? options.Otlp.Protocol;
        options.Tracing.SamplingMode = NormalizeSampler(Get("OTEL_TRACES_SAMPLER")) ?? options.Tracing.SamplingMode;
        if (double.TryParse(Get("OTEL_TRACES_SAMPLER_ARG"), out var ratio))
        {
            options.Tracing.SamplingRatio = ratio;
        }
    }

    private static string? NormalizeSampler(string? value) => value?.ToLowerInvariant() switch
    {
        "always_on" => "AlwaysOn",
        "always_off" => "AlwaysOff",
        "traceidratio" => "TraceIdRatio",
        "parentbased_always_on" => "ParentBasedAlwaysOn",
        "parentbased_traceidratio" => "ParentBasedTraceIdRatio",
        null => null,
        _ => value
    };

    private static void ConfigureExporter(OtlpExporterOptions exporter, OtlpOptions options)
    {
        exporter.Endpoint = options.Endpoint;
        exporter.Protocol = options.Protocol.Equals("HttpProtobuf", StringComparison.OrdinalIgnoreCase)
            || options.Protocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;
    }

    private static void AddIfPresent(ICollection<KeyValuePair<string, object>> attributes, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            attributes.Add(new(key, value));
        }
    }

    private static void AddResourceAttributes(ICollection<KeyValuePair<string, object>> attributes, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return;
        }

        foreach (var part in encoded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator > 0 && separator < part.Length - 1)
            {
                AddIfPresent(attributes, part[..separator], Uri.UnescapeDataString(part[(separator + 1)..]));
            }
        }
    }

    private static string? Get(string name) => System.Environment.GetEnvironmentVariable(name);

    private static string SanitizeSql(string commandText)
    {
        var value = SqlStringLiteral().Replace(commandText, "?");
        value = SqlNumericLiteral().Replace(value, "?");
        value = SqlWhitespace().Replace(value, " ").Trim();
        return value.Length <= 1024 ? value : value[..1024];
    }

    [GeneratedRegex(@"'(?:''|[^'])*'")]
    private static partial Regex SqlStringLiteral();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\b")]
    private static partial Regex SqlNumericLiteral();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SqlWhitespace();
}
