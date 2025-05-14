using KeyCloak.Application.Services.RolesExtractionService;
using System.Security.Claims;
using System.Text.Json;

namespace KeyCloak.Infrastructure.Identity.Services.RolesExtractionService;

public sealed class RoleExtractionService : IRoleExtractionService
{
    public HashSet<string> ExtractRealmRoles(ClaimsPrincipal user)
    {
        return user.Claims
            .Where(c => c.Type == "realm_access")
            .SelectMany(c =>
            {
                try
                {
                    var roles = new List<string>();
                    var doc = JsonDocument.Parse(c.Value);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
                        roles.AddRange(rolesElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
                    return roles;
                }
                catch
                {
                    return new List<string>();
                }
            })
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
