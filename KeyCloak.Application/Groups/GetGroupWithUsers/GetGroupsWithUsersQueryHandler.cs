using KeyCloak.Application.Abstractions.Identity;
using MediatR;
using System.Text.Json;

namespace KeyCloak.Application.Groups.GetGroupWithUsers;

public class GetGroupsWithUsersQueryHandler(IIdentityProviderService identityProviderService)
    : IRequestHandler<GetGroupsWithUsersQuery, List<GroupWithUsersDto>>
{
    public async Task<List<GroupWithUsersDto>> Handle(GetGroupsWithUsersQuery request, CancellationToken cancellationToken)
    {
        var userRoles = request.User?.Claims
            .Where(c => c.Type == "realm_access")
            .SelectMany(c =>
            {
                var roles = new List<string>();
                try
                {
                    var doc = JsonDocument.Parse(c.Value);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
                        roles.AddRange(rolesElement.EnumerateArray().Select(r => r.GetString() ?? string.Empty));
                }
                catch { }
                return roles;
            })
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (userRoles == null || !userRoles.Contains("group-viewer"))
            throw new UnauthorizedAccessException("Access denied. Missing group-viewer role.");

        return await identityProviderService.GetGroupsWithUsersByRolesAsync(request.User, cancellationToken);
    }
}