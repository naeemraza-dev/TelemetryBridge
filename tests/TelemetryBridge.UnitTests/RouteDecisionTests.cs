using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using TelemetryBridge.Core;
using TelemetryBridge.StranglerFacade;

namespace TelemetryBridge.UnitTests;

public sealed class RouteDecisionTests
{
    private static readonly MigrationConfiguration Observe =
        MigrationConfiguration.Default with { Mode = "observe" };

    [Theory]
    [InlineData("/api/orders", "Modern")]
    [InlineData("/api/customers", "Legacy")]
    [InlineData("/unknown/123", "Legacy")]
    public void StablePathsSelectExpectedBackend(string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Equal(expected, RouteDecision.Select(context, Observe, new TestEnvironment()).ToString());
    }

    [Fact]
    public void RolloutBoundariesAreDeterministic()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/payments/authorize";

        var zero = Observe with { Mode = "rollout", PaymentModernPercentage = 0 };
        var all = zero with { PaymentModernPercentage = 100 };

        Assert.Equal(ModernizationTarget.Legacy, RouteDecision.Select(context, zero, new TestEnvironment()));
        Assert.Equal(ModernizationTarget.Modern, RouteDecision.Select(context, all, new TestEnvironment()));
    }

    [Fact]
    public void ApiVersionOverridesPaymentMigrationMode()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/payments/authorize";
        context.Request.Headers["X-Api-Version"] = "2";

        Assert.Equal(
            ModernizationTarget.Modern,
            RouteDecision.Select(context, Observe, new TestEnvironment()));
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = ".";
        public string EnvironmentName { get; set; } = "Production";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
    }
}
