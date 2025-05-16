using KeyCloak.Application.Services.GroupsService;
using MediatR;
using System.Text.Json;

namespace KeyCloak.Application.Groups.GetGroup;

internal sealed class GetFilteredGroupsQueryHandler(IUserGroupQueryService userGroupQueryService)
    : IRequestHandler<GetFilteredGroupsQuery, List<Dictionary<string, object>>>
{
    public async Task<List<Dictionary<string, object>>> Handle(GetFilteredGroupsQuery request, CancellationToken cancellationToken)
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

        return await userGroupQueryService.GetFilteredGroupsAsync(request.User, cancellationToken);
    }
}