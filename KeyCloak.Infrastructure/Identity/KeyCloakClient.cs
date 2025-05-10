using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian.Users;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Net.Http;
using System.Net;

namespace KeyCloak.Infrastructure.Identity;

public sealed class KeyCloakClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
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

    public async Task<string> RegisterUserAsync(UserRepresentation user, string adminToken, CancellationToken cancellationToken = default)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await httpClient.PostAsJsonAsync("users", user, cancellationToken);
        response.EnsureSuccessStatusCode();
        return ExtractUserIdentityIdFromLocationHeader(response);
    }

    public async Task<string> CreateGroupAsync(GroupRepresentation group, string adminToken, CancellationToken cancellationToken)
    {
        var groupData = new
        {
            name = group.Name,
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(groupData), Encoding.UTF8, "application/json");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await httpClient.PostAsync($"groups", jsonContent);

        response.EnsureSuccessStatusCode();
        return ExtractGroupIdentityIdFromLocationHeader(response);
    }
    public async Task<bool> UpdateGroupAsync(GroupRepresentation group, string adminToken, CancellationToken cancellationToken)
    {
        //await CreateGroupIfNotExistsAsync(group.Name, adminToken, cancellationToken).ConfigureAwait(false);
        var updatePayload = new
        {
            name = group.Name
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await httpClient.PutAsJsonAsync($"groups/{group.GroupId}", updatePayload, cancellationToken);
        response.EnsureSuccessStatusCode();
        Guid groupId = group.GroupId ?? Guid.Empty;
        return GroupExistsAsync(groupId,adminToken,cancellationToken).Result;
    }
    public async Task<string> DeleteGroupAsync(Guid groupId, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await httpClient.DeleteAsync($"groups/{groupId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Group with ID '{groupId}' was not found.");
        }
        response.EnsureSuccessStatusCode();
        return groupId.ToString();
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

    public async Task<bool> ForgotPasswordAsync(string email, string adminUrl, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await httpClient.GetAsync($"{adminUrl}/users?email={email}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(cancellationToken: cancellationToken);
        if (users == null || users.Count == 0) return false;

        var userId = users[0]["id"].ToString();
        var actionResponse = await httpClient.PutAsJsonAsync($"{adminUrl}/users/{userId}/execute-actions-email", new[] { "UPDATE_PASSWORD" }, cancellationToken);
        return actionResponse.IsSuccessStatusCode;
    }

    public async Task<bool> ResendConfirmationEmailAsync(string email, string adminUrl, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
    public async Task<string> CreateGroupIfNotExistsAsync(string groupName, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
    public async Task AssignUserToGroupAsync(string userId, string groupId, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await httpClient.PutAsync($"users/{userId}/groups/{groupId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignRealmRoleToUserAsync(string userId, string roleName, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var roleResponse = await httpClient.GetAsync($"roles/{roleName}", cancellationToken);
        roleResponse.EnsureSuccessStatusCode();

        var role = await roleResponse.Content.ReadFromJsonAsync<RoleRepresentation>(cancellationToken: cancellationToken);
        if (role == null) throw new Exception($"Role '{roleName}' not found.");

        var roles = new[] { role };

        var assignResponse = await httpClient.PostAsJsonAsync($"users/{userId}/role-mappings/realm", roles, cancellationToken);
        assignResponse.EnsureSuccessStatusCode();
    }

    public async Task<List<UserDto>> GetUsersByGroupAsync(string groupPath, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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

    public async Task<List<GroupRepresentation>> GetGroupByNameAsync(string groupName, string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
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

    public async Task<List<Dictionary<string, object>>> GetAllGroupsAsync(string adminToken, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

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
                    allGroups[i]["subGroups"] = await ProcessSubGroupsRecursivelyAsync(subGroups, adminToken, cancellationToken);
                }
            }
        }

        return allGroups;
    }
    private async Task<List<Dictionary<string, object>>> ProcessSubGroupsRecursivelyAsync(
        List<Dictionary<string, object>> groups,
        string token,
        CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
                    groups[i]["subGroups"] = await ProcessSubGroupsRecursivelyAsync(subGroups, token, cancellationToken);
                }
                else
                {
                    groups[i]["subGroups"] = new List<Dictionary<string, object>>();
                }
            }
        }
        return groups;
    }
}
