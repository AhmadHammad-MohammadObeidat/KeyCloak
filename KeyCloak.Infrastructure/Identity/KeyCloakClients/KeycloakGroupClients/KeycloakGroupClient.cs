using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Security.Claims;
using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian.Users;
using KeyCloak.Application.Groups.GetGroupWithUsers;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace KeyCloak.Infrastructure.Identity;

public sealed class KeycloakGroupClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private string? AccessToken => httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

    private void SetAuthHeader()
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
    }

    public async Task<string> CreateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var json = JsonSerializer.Serialize(new { name = group.Name });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("groups", content);
        response.EnsureSuccessStatusCode();
        return ExtractGroupIdFromLocation(response);
    }

    public async Task<bool> UpdateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var content = JsonContent.Create(new { name = group.Name });
        var response = await httpClient.PutAsync($"groups/{group.GroupId}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<string> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.DeleteAsync($"groups/{groupId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Group {groupId} not found.");

        response.EnsureSuccessStatusCode();
        return groupId.ToString();
    }

    public async Task AssignUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.PutAsync($"users/{userId}/groups/{groupId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignRealmRoleToUserAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var roleResp = await httpClient.GetAsync($"roles/{roleName}", cancellationToken);
        roleResp.EnsureSuccessStatusCode();

        var role = await roleResp.Content.ReadFromJsonAsync<RoleRepresentation>(cancellationToken: cancellationToken);
        if (role == null) throw new Exception($"Role '{roleName}' not found.");

        var assignResp = await httpClient.PostAsJsonAsync($"users/{userId}/role-mappings/realm", new[] { role }, cancellationToken);
        assignResp.EnsureSuccessStatusCode();
    }

    public async Task<List<GroupRepresentation>> GetGroupByNameAsync(string name, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.GetAsync($"groups?search={name}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<List<GroupRepresentation>>(cancellationToken: cancellationToken);
        return groups?.Where(g => g.Name == name).ToList() ?? new();
    }

    public async Task<List<Dictionary<string, object>>> GetAllGroupsAsync(CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.GetAsync("groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var allGroups = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content, JsonOptions) ?? [];

        for (int i = 0; i < allGroups.Count; i++)
        {
            if (TryGetString(allGroups[i], "id", out var groupId))
            {
                var subGroups = await GetGroupChildrenAsync(groupId, cancellationToken);
                if (subGroups.Any())
                    allGroups[i]["subGroups"] = await ProcessSubGroupsRecursivelyAsync(subGroups, cancellationToken);
            }
        }
        return allGroups;
    }

    public async Task<List<UserDto>> GetUsersByGroupAsync(string groupPath, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.GetAsync("groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<List<GroupRepresentation>>(cancellationToken: cancellationToken);
        var group = groups?.FirstOrDefault(g => g.Name.Equals(groupPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
        if (group == null) return new();

        var userResp = await httpClient.GetAsync($"groups/{group.GroupId}/members", cancellationToken);
        userResp.EnsureSuccessStatusCode();

        var users = await userResp.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken);
        return users?.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            GroupPath = groupPath
        }).ToList() ?? [];
    }

    public async Task<List<Dictionary<string, object>>> GetFilteredGroupsByRolesAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var userRoles = ExtractRoles(user);
        var response = await httpClient.GetAsync("groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var allGroups = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(JsonOptions, cancellationToken) ?? [];
        var result = new List<Dictionary<string, object>>();
        foreach (var group in allGroups)
        {
            var filtered = await FilterGroupByUserRolesAsync(group, userRoles, cancellationToken);
            if (filtered != null) result.Add(filtered);
        }
        return result;
    }

    public async Task<List<GroupWithUsersDto>> GetGroupsWithUsersByRolesAsync(string token, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                     user.FindFirst("sub")?.Value ??
                     user.FindFirst("user_id")?.Value ??
                     user.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrWhiteSpace(userId)) return [];

        SetAuthHeader();
        var groupsResp = await httpClient.GetAsync("groups", cancellationToken);
        groupsResp.EnsureSuccessStatusCode();

        var groups = await groupsResp.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(JsonOptions, cancellationToken) ?? [];
        var result = new List<GroupWithUsersDto>();

        foreach (var group in groups)
        {
            await CollectGroupsRecursively(group, userId, token, result, cancellationToken);
        }
        return result;
    }
    public async Task<string> CreateGroupIfNotExistsAsync(string groupName, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var existingGroups = await GetGroupByNameAsync(groupName, cancellationToken);
        if (existingGroups.Any())
        {
            var existingGroup = existingGroups.FirstOrDefault(g => g.GroupId.HasValue);
            if (existingGroup != null) return existingGroup.GroupId!.Value.ToString();
        }

        var newGroup = new GroupRepresentation(groupName);
        return await CreateGroupAsync(newGroup, cancellationToken);
    }

    private async Task<List<Dictionary<string, object>>> GetGroupChildrenAsync(string groupId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"groups/{groupId}/children", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(JsonOptions, cancellationToken) ?? [];
    }

    private async Task<List<Dictionary<string, object>>> ProcessSubGroupsRecursivelyAsync(List<Dictionary<string, object>> groups, CancellationToken cancellationToken)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (TryGetString(groups[i], "id", out var groupId))
            {
                var subGroups = await GetGroupChildrenAsync(groupId, cancellationToken);
                groups[i]["subGroups"] = subGroups.Any()
                    ? await ProcessSubGroupsRecursivelyAsync(subGroups, cancellationToken)
                    : new List<Dictionary<string, object>>();
            }
        }
        return groups;
    }

    private async Task<Dictionary<string, object>?> FilterGroupByUserRolesAsync(Dictionary<string, object> group, HashSet<string> userRoles, CancellationToken cancellationToken)
    {
        if (!TryGetString(group, "id", out var groupId) || !TryGetString(group, "name", out var groupName))
            return null;

        var subGroups = await GetGroupChildrenAsync(groupId, cancellationToken);
        var matches = subGroups.Where(sg => TryGetString(sg, "name", out var subName) && userRoles.Contains(subName)).ToList();

        if (userRoles.Contains(groupName) || matches.Any())
        {
            group["subGroups"] = matches;
            return group;
        }
        return null;
    }

    private async Task CollectGroupsRecursively(Dictionary<string, object> group, string userId, string token, List<GroupWithUsersDto> result, CancellationToken cancellationToken)
    {
        if (!TryGetString(group, "id", out var groupId) || !TryGetString(group, "name", out var groupName))
            return;

        var membersResponse = await httpClient.GetAsync($"groups/{groupId}/members", cancellationToken);
        membersResponse.EnsureSuccessStatusCode();

        var users = await membersResponse.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken: cancellationToken) ?? [];
        if (users.Any(u => u.Id == userId))
        {
            result.Add(new GroupWithUsersDto { GroupId = groupId, GroupName = groupName, Users = users });
        }

        var children = await GetGroupChildrenAsync(groupId, cancellationToken);
        foreach (var child in children)
        {
            await CollectGroupsRecursively(child, userId, token, result, cancellationToken);
        }
    }

    private static bool TryGetString(Dictionary<string, object> dict, string key, out string result)
    {
        result = string.Empty;
        if (!dict.TryGetValue(key, out var value)) return false;

        return value switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.String => (result = el.GetString() ?? "") != null,
            string str => (result = str) != null,
            _ => false
        };
    }

    private static HashSet<string> ExtractRoles(ClaimsPrincipal user)
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
                        roles.AddRange(rolesElement.EnumerateArray().Select(r => r.GetString() ?? ""));
                    return roles;
                }
                catch { return new List<string>(); }
            })
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractGroupIdFromLocation(HttpResponseMessage response)
    {
        const string segment = "groups/";
        string? location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Location header is missing");

        int index = location.IndexOf(segment, StringComparison.InvariantCultureIgnoreCase);
        return location[(index + segment.Length)..];
    }
}
