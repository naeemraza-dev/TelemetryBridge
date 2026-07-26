using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TelemetryBridge.Core;

namespace TelemetryBridge.IntegrationTests;

public sealed class AdminApiTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory factory;

    public AdminApiTests(AdminApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task OperatorCanReadAndUpdateButCannotRollback()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TelemetryBridge-Admin-Key", "operator-test-key");

        using var read = await client.GetAsync("/api/configuration/migration");
        read.EnsureSuccessStatusCode();
        var current = await read.Content.ReadFromJsonAsync<MigrationConfiguration>();
        Assert.NotNull(current);

        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/configuration/migration")
        {
            Content = JsonContent.Create(new
            {
                mode = "rollout",
                paymentModernPercentage = 25,
                headerRoutingEnabled = false
            })
        };
        update.Headers.TryAddWithoutValidation("If-Match", $"\"{current.Version}\"");
        using var updated = await client.SendAsync(update);
        updated.EnsureSuccessStatusCode();

        using var rollback = new HttpRequestMessage(HttpMethod.Post, $"/api/configuration/rollback/{current.Version}");
        rollback.Headers.TryAddWithoutValidation("If-Match", $"\"{current.Version + 1}\"");
        using var forbidden = await client.SendAsync(rollback);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AdminCanRollbackAndStaleEtagConflicts()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-TelemetryBridge-Admin-Key", "admin-test-key");

        var current = await client.GetFromJsonAsync<MigrationConfiguration>("/api/configuration/migration");
        Assert.NotNull(current);

        if (current.Version == 1)
        {
            using var seed = new HttpRequestMessage(HttpMethod.Put, "/api/configuration/migration")
            {
                Content = JsonContent.Create(new
                {
                    mode = "rollout",
                    paymentModernPercentage = 10,
                    headerRoutingEnabled = false
                })
            };
            seed.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
            using var seeded = await client.SendAsync(seed);
            seeded.EnsureSuccessStatusCode();
            current = await seeded.Content.ReadFromJsonAsync<MigrationConfiguration>();
            Assert.NotNull(current);
        }

        using var rollback = new HttpRequestMessage(HttpMethod.Post, "/api/configuration/rollback/1");
        rollback.Headers.TryAddWithoutValidation("If-Match", $"\"{current.Version}\"");
        using var restored = await client.SendAsync(rollback);
        restored.EnsureSuccessStatusCode();

        using var stale = new HttpRequestMessage(HttpMethod.Post, "/api/configuration/rollback/1");
        stale.Headers.TryAddWithoutValidation("If-Match", $"\"{current.Version}\"");
        using var conflict = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}

public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "telemetrybridge-integration",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(root);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Migration:FilePath"] = Path.Combine(root, "migration.json"),
                ["Security:AdminKey"] = "admin-test-key",
                ["Security:OperatorKey"] = "operator-test-key",
                ["TelemetryBridge:Enabled"] = "false"
            }));
        builder.ConfigureServices(services =>
            services.AddLogging(logging => logging.ClearProviders()));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
