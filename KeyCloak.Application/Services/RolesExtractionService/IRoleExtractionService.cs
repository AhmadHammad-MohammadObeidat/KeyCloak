using System.Security.Claims;

namespace KeyCloak.Application.Services.RolesExtractionService;

public interface IRoleExtractionService
{
    HashSet<string> ExtractRealmRoles(ClaimsPrincipal user);
}
