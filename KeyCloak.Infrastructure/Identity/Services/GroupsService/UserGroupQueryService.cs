using KeyCloak.Application.Groups.GetGroupWithUsers;
using KeyCloak.Application.Services.GroupsService;
using KeyCloak.Application.Services.RolesExtractionService;
using KeyCloak.Domian.Users;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace KeyCloak.Infrastructure.Identity.Services.GroupsService;

public sealed class UserGroupQueryService(KeycloakGroupClient _keycloakGroupClient, IRoleExtractionService _roleExtractor, ILogger<UserGroupQueryService> _logger) : IUserGroupQueryService
{
    public async Task<List<UserDto>> GetUsersInCallerGroupAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var groupClaim = user.Claims.FirstOrDefault(c => c.Type == "groups")?.Value;
        return string.IsNullOrWhiteSpace(groupClaim)
            ? new List<UserDto>()
            : await _keycloakGroupClient.GetUsersByGroupAsync(groupClaim, cancellationToken);
    }

    public async Task<List<Dictionary<string, object>>> GetAllGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await _keycloakGroupClient.GetAllGroupsAsync(cancellationToken);
    }

    public async Task<List<Dictionary<string, object>>> GetFilteredGroupsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var roles = _roleExtractor.ExtractRealmRoles(user);
        if (!roles.Contains("group-viewer"))
            throw new UnauthorizedAccessException("Missing group-viewer role.");
        return await _keycloakGroupClient.GetFilteredGroupsByRolesAsync(user, cancellationToken);
    }

    public async Task<List<GroupWithUsersDto>> GetGroupsWithUsersByRolesAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var roles = _roleExtractor.ExtractRealmRoles(user);
        if (!roles.Contains("group-viewer"))
            throw new UnauthorizedAccessException("Missing group-viewer role.");
        var token = string.Empty;
        return await _keycloakGroupClient.GetGroupsWithUsersByRolesAsync(token, user, cancellationToken);
    }
}
