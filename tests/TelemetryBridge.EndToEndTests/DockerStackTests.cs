using System.Net.Http.Json;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TelemetryBridge.EndToEndTests;

public sealed class DockerStackTests
{
    [Fact]
    public async Task FacadeCreatesOrderThroughModernAndInternalServices()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("TELEMETRYBRIDGE_E2E"),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        using var source = new ActivitySource("TelemetryBridge.E2E.Browser");
        using var provider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("telemetrybridge-browser-e2e"))
            .AddSource(source.Name)
            .AddOtlpExporter(exporter => exporter.Endpoint = new Uri("http://localhost:4317"))
            .Build();
        using var browserSpan = source.StartActivity("ui.order.submit", ActivityKind.Client);
        Assert.NotNull(browserSpan);

        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
        var traceId = browserSpan.TraceId.ToHexString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { channel = "web" })
        };
        request.Headers.TryAddWithoutValidation("traceparent", browserSpan.Id);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.NotEmpty(values);
        browserSpan.Stop();
        Assert.True(provider.ForceFlush(10_000));

        using var tempo = new HttpClient { BaseAddress = new Uri("http://localhost:3200") };
        string? traceJson = null;
        var requiredTraceContent = new[]
        {
            "telemetrybridge-browser-e2e",
            "ui.order.submit",
            "telemetrybridge-strangler-facade",
            "strangler.route",
            "telemetrybridge-modern-api",
            "telemetrybridge-internal-api",
            "telemetrybridge-worker",
            "order.created process",
            "db.system.name"
        };

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            using var traceResponse = await tempo.GetAsync($"/api/traces/{traceId}");
            if (traceResponse.IsSuccessStatusCode)
            {
                traceJson = await traceResponse.Content.ReadAsStringAsync();
                if (requiredTraceContent.All(
                    expected => traceJson.Contains(expected, StringComparison.Ordinal)))
                {
                    break;
                }
            }
        }

        Assert.NotNull(traceJson);
        foreach (var expected in requiredTraceContent)
        {
            Assert.Contains(expected, traceJson);
        }

        Assert.DoesNotContain("authorization", traceJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DirectNLogRecordIsSearchableByTraceId()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("TELEMETRYBRIDGE_E2E"),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        using var activity = new Activity("legacy-client")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        request.Headers.TryAddWithoutValidation("traceparent", activity.Id);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var traceId = activity.TraceId.ToHexString();
        using var loki = new HttpClient { BaseAddress = new Uri("http://localhost:3100") };
        const string expectedMessage = "Legacy customer list executed";
        var query = Uri.EscapeDataString(
            $"{{service_name=\"telemetrybridge-legacy-api\"}} |= \"{expectedMessage}\"");
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            using var logs = await loki.GetAsync(
                $"/loki/api/v1/query_range?query={query}&limit=1000&direction=backward");
            if (logs.IsSuccessStatusCode)
            {
                var logsJson = await logs.Content.ReadAsStringAsync();
                if (logsJson.Contains(expectedMessage, StringComparison.Ordinal)
                    && logsJson.Contains(traceId, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        Assert.Fail($"No direct NLog record was found for trace {traceId}.");
    }
}
