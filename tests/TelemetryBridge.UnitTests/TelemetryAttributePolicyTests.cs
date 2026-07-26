using TelemetryBridge.Core;

namespace TelemetryBridge.UnitTests;

public sealed class TelemetryAttributePolicyTests
{
    [Theory]
    [InlineData("/api/orders?access_token=secret", "/api/orders")]
    [InlineData("https://example.test/path#fragment", "https://example.test/path")]
    [InlineData("/safe", "/safe")]
    public void RemoveQueryStringRemovesSensitiveComponents(string value, string expected)
    {
        Assert.Equal(expected, TelemetryAttributePolicy.RemoveQueryString(value));
    }

    [Theory]
    [InlineData("/api/orders/12345", "/api/orders/{id}")]
    [InlineData("/api/orders/0192fdf1-2f5f-7209-a826-8eb9d15ca63b", "/api/orders/{id}")]
    [InlineData("/health", "/health")]
    public void NormalizeRouteMasksIdentifiers(string route, string expected)
    {
        Assert.Equal(expected, TelemetryAttributePolicy.NormalizeRoute(route));
    }

    [Fact]
    public void SanitizeDropsDeniedAndUnknownAttributes()
    {
        var policy = new TelemetryAttributePolicy();

        Assert.Null(policy.Sanitize("authorization", "Bearer secret"));
        Assert.Null(policy.Sanitize("customer.email", "person@example.test"));
        Assert.Equal("create", policy.Sanitize("telemetrybridge.operation.type", "create"));
    }

    [Fact]
    public void SanitizeBoundsAttributeLength()
    {
        var policy = new TelemetryAttributePolicy(maximumValueLength: 16);

        var result = policy.Sanitize("telemetrybridge.feature.name", new string('a', 100));

        Assert.NotNull(result);
        Assert.Equal(16, result.Length);
    }
}
