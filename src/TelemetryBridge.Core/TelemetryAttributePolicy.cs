using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace TelemetryBridge.Core;

/// <summary>Applies low-cardinality and sensitive-data safeguards to custom telemetry attributes.</summary>
public sealed partial class TelemetryAttributePolicy
{
    private static readonly FrozenSet<string> DefaultAllowed = new[]
    {
        "http.request.method",
        "http.response.status_code",
        "http.route",
        "server.address",
        "db.system.name",
        "db.operation.name",
        "error.type",
        "telemetrybridge.workflow.name",
        "telemetrybridge.feature.name",
        "telemetrybridge.operation.type"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DefaultDenied = new[]
    {
        "authorization",
        "cookie",
        "set-cookie",
        "password",
        "access_token",
        "refresh_token",
        "db.query.text",
        "db.statement",
        "url.full",
        "http.url",
        "http.request.body",
        "http.response.body",
        "user.id",
        "user.email"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a policy with secure defaults.</summary>
    public TelemetryAttributePolicy(
        IEnumerable<string>? allowedKeys = null,
        IEnumerable<string>? deniedKeys = null,
        int maximumValueLength = 256)
    {
        if (maximumValueLength is < 16 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumValueLength), "Value length must be between 16 and 4096.");
        }

        AllowedKeys = (allowedKeys ?? DefaultAllowed).ToFrozenSet(StringComparer.Ordinal);
        DeniedKeys = (deniedKeys ?? DefaultDenied).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        MaximumValueLength = maximumValueLength;
    }

    /// <summary>Gets the explicitly allowed custom attribute names.</summary>
    public FrozenSet<string> AllowedKeys { get; }

    /// <summary>Gets the explicitly denied attribute names.</summary>
    public FrozenSet<string> DeniedKeys { get; }

    /// <summary>Gets the maximum emitted string length.</summary>
    public int MaximumValueLength { get; }

    /// <summary>Returns a safe value when the key is allowed; otherwise returns <see langword="null"/>.</summary>
    public string? Sanitize(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (value is null || DeniedKeys.Contains(key) || !AllowedKeys.Contains(key))
        {
            return null;
        }

        var sanitized = RemoveQueryString(value.Trim());
        sanitized = SecretPattern().Replace(sanitized, "[REDACTED]");
        return sanitized.Length <= MaximumValueLength ? sanitized : sanitized[..MaximumValueLength];
    }

    /// <summary>Removes query strings and fragments from absolute or relative URLs.</summary>
    public static string RemoveQueryString(string value)
    {
        var end = value.IndexOfAny(['?', '#']);
        return end < 0 ? value : value[..end];
    }

    /// <summary>Replaces identifier-like path segments with a stable placeholder.</summary>
    public static string NormalizeRoute(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var clean = RemoveQueryString(path);
        return IdentifierSegment().Replace(clean, "/{id}");
    }

    [GeneratedRegex(@"(?i)(bearer\s+[a-z0-9._~+/\-=]+|(?:password|access_token|refresh_token)=([^&\s]+))")]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"/(?:\d{2,}|[0-9a-f]{8}-[0-9a-f-]{27,})(?=/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex IdentifierSegment();
}
