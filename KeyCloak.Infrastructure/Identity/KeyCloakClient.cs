using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian.Users;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Net.Http;
using System.Net;
using System.Security.Claims;
using KeyCloak.Application.Groups.GetGroupWithUsers;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace KeyCloak.Infrastructure.Identity;

public sealed class KeyCloakClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    #region Authentication

    public async Task<TokenResponse> UserLoginAsync(string username, string password, string publicClientId, string scope,
                                                    string grantType, string tokenUrl, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", publicClientId),
            new KeyValuePair<string, string>("scope", scope),
            new KeyValuePair<string, string>("grant_type", grantType),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        });

        using var authRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(tokenUrl)) { Content = content };
        using var httpResponseMessage = await httpClient.SendAsync(authRequest, cancellationToken);
        httpResponseMessage.EnsureSuccessStatusCode();
        return await httpResponseMessage.Content.ReadFromJsonAsync<TokenResponse>() ?? new TokenResponse();
    }


    public async Task<string> RegisterUserAsync(UserRepresentation user, CancellationToken cancellationToken = default)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.PostAsJsonAsync("users", user, cancellationToken);
        response.EnsureSuccessStatusCode();
        return ExtractUserIdentityIdFromLocationHeader(response);
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, string publicClientId, string grantType, string tokenUrl, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", publicClientId),
            new KeyValuePair<string, string>("grant_type", grantType),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(tokenUrl)) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>() ?? new TokenResponse();
    }

    public async Task<bool> ForgotPasswordAsync(string email, string adminUrl, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync($"{adminUrl}/users?email={email}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(cancellationToken: cancellationToken);
        if (users == null || users.Count == 0) return false;

        var userId = users[0]["id"].ToString();
        var actionResponse = await httpClient.PutAsJsonAsync($"{adminUrl}/users/{userId}/execute-actions-email", new[] { "UPDATE_PASSWORD" }, cancellationToken);
        return actionResponse.IsSuccessStatusCode;
    }

    public async Task<bool> ResendConfirmationEmailAsync(string email, string adminUrl, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync($"{adminUrl}/users?email={email}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(cancellationToken: cancellationToken);
        if (users == null || users.Count == 0) return false;

        var userId = users[0]["id"].ToString();
        var actionResponse = await httpClient.PutAsJsonAsync($"{adminUrl}/users/{userId}/execute-actions-email", new[] { "VERIFY_EMAIL" }, cancellationToken);
        return actionResponse.IsSuccessStatusCode;
    }

    public async Task<string> GetAdminAccessTokenAsync(string tokenUrl, string clientId, string clientSecret, string username, string password, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        return tokenResponse?.AccessToken ?? throw new InvalidOperationException("Failed to get access token");
    }

    private static string ExtractUserIdentityIdFromLocationHeader(HttpResponseMessage response)
    {
        const string segment = "users/";
        string? location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Location header is missing");

        int index = location.IndexOf(segment, StringComparison.InvariantCultureIgnoreCase);
        return location[(index + segment.Length)..];
    }

    private static string ExtractGroupIdentityIdFromLocationHeader(HttpResponseMessage response)
    {
        const string segment = "groups/";
        string? location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Location header is missing");

        int index = location.IndexOf(segment, StringComparison.InvariantCultureIgnoreCase);
        return location[(index + segment.Length)..];
    }

    #endregion


    #region Groups

    public async Task<string> CreateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken)
    {
        var groupData = new
        {
            name = group.Name,
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(groupData), Encoding.UTF8, "application/json");

        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.PostAsync($"groups", jsonContent);

        response.EnsureSuccessStatusCode();
        return ExtractGroupIdentityIdFromLocationHeader(response);
    }
    public async Task<bool> UpdateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken)
    {
        var updatePayload = new
        {
            name = group.Name
        };

        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.PutAsJsonAsync($"groups/{group.GroupId}", updatePayload, cancellationToken);
        response.EnsureSuccessStatusCode();
        Guid groupId = group.GroupId ?? Guid.Empty;
        return GroupExistsAsync(groupId, accessToken, cancellationToken).Result;
    }
    public async Task<string> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await httpClient.DeleteAsync($"groups/{groupId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Group with ID '{groupId}' was not found.");
        }
        response.EnsureSuccessStatusCode();
        return groupId.ToString();
    }

    public async Task<List<Dictionary<string, object>>> GetFilteredGroupsByRolesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userRoles = ExtractRealmRolesFromClaims(user);

        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync("groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var allGroups = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content, DefaultJsonSerializerOptions) ?? [];

        var result = new List<Dictionary<string, object>>();
        foreach (var group in allGroups)
        {
            var filtered = await FilterGroupByUserRolesAsync(group, userRoles, cancellationToken);
            if (filtered != null) result.Add(filtered);
        }

        return result;
    }

    public async Task<List<GroupWithUsersDto>> GetGroupsWithUsersByRolesAsync(
    string token,
    ClaimsPrincipal user,
    CancellationToken cancellationToken)
    {
        var result = new List<GroupWithUsersDto>();

        var currentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user.FindFirst("sub")?.Value
                   ?? user.FindFirst("user_id")?.Value
                   ?? user.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
            return result;

        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"]
            .ToString()
            .Replace("Bearer ", "");


        if (accessToken is null)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        var response = await httpClient.GetAsync("groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(DefaultJsonSerializerOptions, cancellationToken) ?? [];

        foreach (var group in groups)
        {
            await CollectGroupsRecursively(group, currentUserId, token, result, cancellationToken);
        }

        return result;
    }

    private async Task CollectGroupsRecursively(
        Dictionary<string, object> group,
        string currentUserId,
        string token,
        List<GroupWithUsersDto> result,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(group, "id", out var groupId) || !TryGetString(group, "name", out var groupName))
            return;

        var membersResponse = await httpClient.GetAsync($"groups/{groupId}/members", cancellationToken);
        membersResponse.EnsureSuccessStatusCode();

        var users = await membersResponse.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken: cancellationToken) ?? [];

        if (users.Any(u => u.Id == currentUserId))
        {
            result.Add(new GroupWithUsersDto
            {
                GroupId = groupId,
                GroupName = groupName,
                Users = users
            });
        }

        var childrenResponse = await httpClient.GetAsync($"groups/{groupId}/children", cancellationToken);
        childrenResponse.EnsureSuccessStatusCode();

        var children = await childrenResponse.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(DefaultJsonSerializerOptions, cancellationToken) ?? [];
        foreach (var child in children)
        {
            await CollectGroupsRecursively(child, currentUserId, token, result, cancellationToken);
        }
    }

    private static HashSet<string> ExtractRealmRolesFromClaims(ClaimsPrincipal user)
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
                        roles.AddRange(rolesElement.EnumerateArray().Select(r => r.GetString() ?? string.Empty));
                    return roles;
                }
                catch { return new List<string>(); }
            })
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetString(Dictionary<string, object> dict, string key, out string result)
    {
        result = string.Empty;

        if (dict.TryGetValue(key, out var value))
        {
            switch (value)
            {
                case JsonElement el when el.ValueKind == JsonValueKind.String:
                    result = el.GetString() ?? string.Empty;
                    return true;
                case string str:
                    result = str;
                    return true;
            }
        }

        return false;
    }
    private async Task<Dictionary<string, object>?> FilterGroupByUserRolesAsync(
    Dictionary<string, object> group,
    HashSet<string> userRoles,
    CancellationToken cancellationToken)
    {
        if (!TryGetString(group, "id", out var groupId) || !TryGetString(group, "name", out var groupName))
            return null;


        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var subResponse = await httpClient.GetAsync($"groups/{groupId}/children", cancellationToken);
        subResponse.EnsureSuccessStatusCode();

        var subGroups = await subResponse.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(DefaultJsonSerializerOptions, cancellationToken) ?? [];

        var matchingSubGroups = subGroups
            .Where(sg => TryGetString(sg, "name", out var subName) && userRoles.Contains(subName))
            .ToList();

        if (userRoles.Contains(groupName) || matchingSubGroups.Count > 0)
        {
            group["subGroups"] = matchingSubGroups;
            return group;
        }

        return null;
    }

    private async Task<List<Dictionary<string, object>>> ProcessSubGroupsRecursivelyAsync(
     List<Dictionary<string, object>> groups,
     string accessToken,
     CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].TryGetValue("id", out var groupIdObj))
            {
                string groupId;

                if (groupIdObj is JsonElement idElement && idElement.ValueKind == JsonValueKind.String)
                {
                    groupId = idElement.GetString() ?? string.Empty;
                }
                else if (groupIdObj is string idString)
                {
                    groupId = idString;
                }
                else
                {
                    continue;
                }
                var response = await httpClient.GetAsync($"groups/{groupId}/children", cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var subGroups = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content, DefaultJsonSerializerOptions)
                               ?? new List<Dictionary<string, object>>();

                if (subGroups.Count > 0)
                {
                    groups[i]["subGroups"] = await ProcessSubGroupsRecursivelyAsync(subGroups, accessToken, cancellationToken);
                }
                else
                {
                    groups[i]["subGroups"] = new List<Dictionary<string, object>>();
                }
            }
        }
        return groups;
    }

    public async Task<List<Dictionary<string, object>>> GetAllGroupsAsync(CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync($"groups", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var allGroups = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content, DefaultJsonSerializerOptions)
                        ?? new List<Dictionary<string, object>>();

        for (int i = 0; i < allGroups.Count; i++)
        {
            if (allGroups[i].TryGetValue("id", out var groupIdObj) && groupIdObj is JsonElement idElement && idElement.ValueKind == JsonValueKind.String)
            {
                string groupId = idElement.GetString() ?? string.Empty;

                var subGroupsResponse = await httpClient.GetAsync($"groups/{groupId}/children", cancellationToken);
                subGroupsResponse.EnsureSuccessStatusCode();

                var subGroupsContent = await subGroupsResponse.Content.ReadAsStringAsync(cancellationToken);
                var subGroups = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(subGroupsContent, DefaultJsonSerializerOptions)
                               ?? new List<Dictionary<string, object>>();
                if (subGroups.Count > 0)
                {
                    allGroups[i]["subGroups"] = await ProcessSubGroupsRecursivelyAsync(subGroups, accessToken, cancellationToken);
                }
            }
        }

        return allGroups;
    }

    public async Task<List<GroupRepresentation>> GetGroupByNameAsync(string groupName, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await httpClient.GetAsync($"groups?search={groupName}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var groups = await response.Content.ReadFromJsonAsync<List<GroupRepresentation>>(cancellationToken: cancellationToken);
        return groups?.Select(g => new GroupRepresentation(g.Name, g.GroupId)).ToList() ?? new List<GroupRepresentation>();
    }

    public async Task<bool> GroupExistsAsync(Guid groupId, string accessToken, CancellationToken cancellationToken)
    {
        string groupIdString = groupId.ToString();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var idResponse = await httpClient.GetAsync($"groups/{groupIdString}", cancellationToken);
        if (idResponse.IsSuccessStatusCode)
            return true;
        var listResponse = await httpClient.GetAsync("groups", cancellationToken);
        if (!listResponse.IsSuccessStatusCode)
            return false;
        var groups = await listResponse.Content.ReadFromJsonAsync<List<GroupRepresentation>>(cancellationToken: cancellationToken);
        return groups?.Any(g => g.Name.Equals(groupIdString, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    public async Task<string> CreateGroupIfNotExistsAsync(string groupName, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var groupsResponse = await httpClient.GetAsync("groups", cancellationToken);
        groupsResponse.EnsureSuccessStatusCode();

        var groups = await groupsResponse.Content.ReadFromJsonAsync<List<GroupRepresentation>>(cancellationToken: cancellationToken);
        var existing = groups?.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (existing != null && existing.GroupId.HasValue) return existing.GroupId.Value.ToString();
        var groupData = new
        {
            name = groupName
        };
        var createResponse = await httpClient.PostAsJsonAsync("groups", groupData, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        return ExtractGroupIdentityIdFromLocationHeader(createResponse);
    }
    public async Task AssignUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await httpClient.PutAsync($"users/{userId}/groups/{groupId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignRealmRoleToUserAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var roleResponse = await httpClient.GetAsync($"roles/{roleName}", cancellationToken);
        roleResponse.EnsureSuccessStatusCode();

        var role = await roleResponse.Content.ReadFromJsonAsync<RoleRepresentation>(cancellationToken: cancellationToken);
        if (role == null) throw new Exception($"Role '{roleName}' not found.");

        var roles = new[] { role };

        var assignResponse = await httpClient.PostAsJsonAsync($"users/{userId}/role-mappings/realm", roles, cancellationToken);
        assignResponse.EnsureSuccessStatusCode();
    }

    public async Task<List<UserDto>> GetUsersByGroupAsync(string groupPath, CancellationToken cancellationToken)
    {

        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var groupResponse = await httpClient.GetAsync("groups", cancellationToken);
        groupResponse.EnsureSuccessStatusCode();

        var groups = await groupResponse.Content.ReadFromJsonAsync<List<GroupRepresentation>>(cancellationToken: cancellationToken);
        var targetGroup = groups!.FirstOrDefault(g => g.Name.Equals(groupPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

        if (targetGroup == null)
            return new List<UserDto>();

        var usersResponse = await httpClient.GetAsync($"groups/{targetGroup.GroupId}/members", cancellationToken);
        usersResponse.EnsureSuccessStatusCode();

        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken);
        return users?.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            GroupPath = groupPath
        }).ToList() ?? new List<UserDto>();
    }


    #endregion
}
