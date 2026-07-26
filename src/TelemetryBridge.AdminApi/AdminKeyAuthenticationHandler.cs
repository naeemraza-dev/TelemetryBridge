using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TelemetryBridge.AdminApi;

internal sealed class AdminKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "AdminKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-TelemetryBridge-Admin-Key", out var supplied)
            || supplied.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(supplied[0] ?? string.Empty);
        var adminKey = configuration["Security:AdminKey"];
        var operatorKey = configuration["Security:OperatorKey"];
        var role = Matches(suppliedBytes, adminKey) ? "Admin"
            : Matches(suppliedBytes, operatorKey) ? "Operator"
            : null;
        if (role is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid administration key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, role.ToLowerInvariant()), new Claim(ClaimTypes.Role, role)],
            AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme)));
    }

    private static bool Matches(byte[] supplied, string? expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return supplied.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(supplied, expectedBytes);
    }
}
