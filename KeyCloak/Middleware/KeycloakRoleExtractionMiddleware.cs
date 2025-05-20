using System.Security.Claims;
using System.Text.Json;

namespace KeyCloak.Api.Middleware;

public class KeycloakRoleExtractionMiddleware(RequestDelegate _next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var identity = context.User.Identity as ClaimsIdentity;
        var realmAccess = context.User.FindFirst("realm_access")?.Value;

        if (!string.IsNullOrEmpty(realmAccess))
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
            {
                foreach (var role in rolesElement.EnumerateArray())
                {
                    var roleName = role.GetString();
                    if (!string.IsNullOrEmpty(roleName))
                        identity?.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }

        await _next(context);
    }
}