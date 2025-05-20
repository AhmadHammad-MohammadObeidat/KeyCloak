using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KeyCloak.Api.Middleware;

public class KeycloakAuthorizationMiddleware(RequestDelegate _next, ILogger<KeycloakAuthorizationMiddleware> _logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var permissions = new List<(string Resource, List<string> Scopes)>();

        var authClaim = user.FindFirst("authorization")?.Value;
        if (!string.IsNullOrWhiteSpace(authClaim))
        {
            try
            {
                using var json = JsonDocument.Parse(authClaim);
                if (json.RootElement.TryGetProperty("permissions", out var permissionArray))
                {
                    foreach (var p in permissionArray.EnumerateArray())
                    {
                        var resource = p.GetProperty("rsname").GetString() ?? "";
                        var scopes = p.TryGetProperty("scopes", out var s) && s.ValueKind == JsonValueKind.Array
                            ? s.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToList()!
                            : new List<string>();

                        permissions.Add((resource, scopes));
                        _logger.LogInformation("🔐 Keycloak Permission: {Resource} → {Scopes}", resource, string.Join(", ", scopes));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Keycloak permissions");
            }
        }

        context.Items["KeycloakPermissions"] = permissions;

        await _next(context);
    }
}
