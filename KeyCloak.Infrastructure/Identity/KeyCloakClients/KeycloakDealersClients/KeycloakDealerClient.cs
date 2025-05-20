using KeyCloak.Domian.Dealers;
using KeyCloak.Domian;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using KeyCloak.Domian.Users;
using KeyCloak.Domian.AccountsGroups;

namespace KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakDealersClients;

public sealed class KeycloakDealerClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string? AccessToken => httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

    private void SetAuthHeader()
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
    }

    public async Task<Result<string>> UpdateUserAsync(string userId, string username, string firstName, string lastName, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var content = JsonContent.Create(new { username, firstName, lastName });
        var response = await httpClient.PutAsync($"users/{userId}", content, cancellationToken);

        return response.IsSuccessStatusCode
            ? Result.Success("User updated")
            : Result.Failure<string>(new Error("Keycloak.User.Update", $"Failed to update user: {response.StatusCode}", ErrorType.Failure));
    }

    public async Task<Result<string>> DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.DeleteAsync($"users/{userId}", cancellationToken);

        return response.IsSuccessStatusCode
            ? Result.Success("User deleted")
            : Result.Failure<string>(new Error("Keycloak.User.Delete", $"Failed to delete user: {response.StatusCode}", ErrorType.Failure));
    }

    public async Task<Result<string>> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var payload = new[]
        {
            new
            {
                type = "password",
                value = newPassword,
                temporary = false
            }
        };

        var response = await httpClient.PutAsJsonAsync($"users/{userId}/reset-password", payload, cancellationToken);

        return response.IsSuccessStatusCode
            ? Result.Success("Password reset")
            : Result.Failure<string>(new Error("Keycloak.User.PasswordReset", $"Failed to reset password: {response.StatusCode}", ErrorType.Failure));
    }

    public async Task<Result<string>> MoveUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await httpClient.PutAsync($"users/{userId}/groups/{groupId}", null, cancellationToken);

        return response.IsSuccessStatusCode
            ? Result.Success("User moved to group")
            : Result.Failure<string>(new Error("Keycloak.User.GroupMove", $"Failed to move user: {response.StatusCode}", ErrorType.Failure));
    }

    public async Task<List<GroupWithAdminsDto>> GetGroupsWithAdminsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var result = new List<GroupWithAdminsDto>();

        var roles = new List<string>();

        var realmAccessClaim = user.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccessClaim))
        {
            using var json = JsonDocument.Parse(realmAccessClaim);
            if (json.RootElement.TryGetProperty("roles", out var rolesElement) &&
                rolesElement.ValueKind == JsonValueKind.Array)
            {
                roles = rolesElement.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            }
        }

        var userId = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return result;

        if (roles.Contains("super-admin"))
        {
            // Super-admin: Fetch all users and group memberships
            var usersResponse = await httpClient.GetAsync("users", cancellationToken);
            usersResponse.EnsureSuccessStatusCode();
            var users = await usersResponse.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken) ?? [];

            var groupMap = new Dictionary<string, GroupWithAdminsDto>();

            foreach (var userRecord in users)
            {
                var rolesResponse = await httpClient.GetAsync($"users/{userRecord.Id}/role-mappings/realm", cancellationToken);
                rolesResponse.EnsureSuccessStatusCode();
                var userRoles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken: cancellationToken) ?? [];

                bool isAdmin = userRoles.Any(r =>
                    r.Name.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                    r.Name.Equals("super-admin", StringComparison.OrdinalIgnoreCase));

                if (!isAdmin)
                    continue;

                var groupsResponse = await httpClient.GetAsync($"users/{userRecord.Id}/groups", cancellationToken);
                groupsResponse.EnsureSuccessStatusCode();
                var userGroups = await groupsResponse.Content.ReadFromJsonAsync<List<KeycloakGroup>>(cancellationToken: cancellationToken) ?? [];

                foreach (var group in userGroups)
                {
                    if (!groupMap.ContainsKey(group.Id))
                    {
                        groupMap[group.Id] = new GroupWithAdminsDto
                        {
                            GroupId = group.Id,
                            GroupName = group.Name,
                            Dealers = new List<DealerDto>()
                        };
                    }

                    groupMap[group.Id].Dealers.Add(new DealerDto
                    {
                        DealerId = userRecord.Id,
                        DealerName = userRecord.Username,
                        FirstName = userRecord.FirstName,
                        LastName = userRecord.LastName,
                        Email = userRecord.Email,
                        GroupId = group.Id,
                        GroupName = group.Name,
                        Roles = userRoles.Select(r => r.Name).ToList()
                    });
                }
            }

            return groupMap.Values.ToList();
        }
        else
        {
            // Regular admin: Return their own groups and admins within those groups
            var groupsResponse = await httpClient.GetAsync($"users/{userId}/groups", cancellationToken);
            groupsResponse.EnsureSuccessStatusCode();
            var userGroups = await groupsResponse.Content.ReadFromJsonAsync<List<KeycloakGroup>>(cancellationToken: cancellationToken) ?? [];

            foreach (var group in userGroups)
            {
                var groupDetailsResponse = await httpClient.GetAsync($"groups/{group.Id}", cancellationToken);
                groupDetailsResponse.EnsureSuccessStatusCode();

                var groupDetails = await groupDetailsResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>(JsonOptions, cancellationToken);
                if (groupDetails is not null)
                {
                    await CollectGroupWithAdminsRecursively(groupDetails, result, cancellationToken);
                }
            }

            return result;
        }
    }

    public async Task<List<DealerWithGroupsDto>> GetDealersWithGroupsAsync(ClaimsPrincipal userPrincipal, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var result = new Dictionary<string, DealerWithGroupsDto>();

        var userId = userPrincipal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var roles = ExtractRealmRoles(userPrincipal);
        var isSuperAdmin = roles.Contains("super-admin");
        var isAdmin = isSuperAdmin || roles.Contains("admin");

        if (!isAdmin)
            return [];

        try
        {
            var adminUsers = await GetUsersByRoleAsync("admin", cancellationToken);
            var superAdminUsers = await GetUsersByRoleAsync("super-admin", cancellationToken);

            // Union of both sets by user ID
            var uniqueUsers = adminUsers.Concat(superAdminUsers)
                                        .GroupBy(u => u.Id)
                                        .Select(g => g.First())
                                        .ToList();

            foreach (var user in uniqueUsers)
            {
                var dealerName = string.IsNullOrEmpty(user.FirstName) && string.IsNullOrEmpty(user.LastName)
                    ? user.Username
                    : $"{user.FirstName} {user.LastName}";

                var dealer = new DealerWithGroupsDto
                {
                    DealerId = user.Id,
                    DealerName = dealerName,
                    Groups = new List<GroupInfoDto>()
                };

                var userGroupsResponse = await httpClient.GetAsync($"users/{user.Id}/groups", cancellationToken);
                if (userGroupsResponse.IsSuccessStatusCode)
                {
                    var userGroups = await userGroupsResponse.Content.ReadFromJsonAsync<List<KeycloakGroup>>(cancellationToken: cancellationToken) ?? [];

                    foreach (var group in userGroups)
                    {
                        dealer.Groups.Add(new GroupInfoDto
                        {
                            GroupId = group.Id,
                            GroupName = group.Name,
                            GroupPath = group.Path
                        });
                    }
                }

                result[user.Id] = dealer;
            }

            return result.Values.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching dealers with groups: {ex.Message}");
            return [];
        }
    }

    private List<string> ExtractRealmRoles(ClaimsPrincipal user)
    {
        var roles = new List<string>();
        var realmAccess = user.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccess))
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var rolesElem) && rolesElem.ValueKind == JsonValueKind.Array)
            {
                roles = rolesElem.EnumerateArray().Select(r => r.GetString()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList()!;
            }
        }
        return roles;
    }

    private async Task<List<UserRepresentation>> GetUsersByRoleAsync(string roleName, CancellationToken cancellationToken)
{
    SetAuthHeader();

    var response = await httpClient.GetAsync($"roles/{roleName}/users", cancellationToken);
    response.EnsureSuccessStatusCode();

    var users = await response.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken) ?? [];
    return users;
}

    private async Task CollectGroupWithAdminsRecursively(
    Dictionary<string, object> group,
    List<GroupWithAdminsDto> result,
    CancellationToken cancellationToken,
    string? parentGroupId = null,
    string? parentGroupName = null)
    {
        if (!TryGetGroupId(group, out var groupId) || !TryGetGroupName(group, out var groupName))
            return;

        var dealers = await GetAdminsInGroupAsync(groupId, cancellationToken);

        if (dealers.Any())
        {
            foreach (var dealer in dealers)
            {
                dealer.GroupId = parentGroupId ?? groupId;
                dealer.GroupName = parentGroupName ?? groupName;
                dealer.SubGroupId = parentGroupId != null ? groupId : null;
                dealer.SubGroupName = parentGroupId != null ? groupName : null;
            }

            result.Add(new GroupWithAdminsDto
            {
                GroupId = parentGroupId ?? groupId,
                GroupName = parentGroupName ?? groupName,
                Dealers = dealers
            });
        }

        // Process subgroups recursively
        if (group.TryGetValue("subGroups", out var subGroupsObj) && subGroupsObj is JsonElement subGroupsElement && subGroupsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var subGroup in subGroupsElement.EnumerateArray())
            {
                var subGroupDict = JsonSerializer.Deserialize<Dictionary<string, object>>(subGroup.GetRawText(), JsonOptions);
                if (subGroupDict is not null)
                {
                    await CollectGroupWithAdminsRecursively(subGroupDict, result, cancellationToken, groupId, groupName);
                }
            }
        }
    }

    private async Task CollectAdminsInGroupRecursively(
    Dictionary<string, object> group,
    Dictionary<string, DealerWithGroupsDto> result,
    HashSet<string> processedGroups,
    CancellationToken cancellationToken,
    string? parentPath = null)
    {
        if (!TryGetGroupId(group, out var groupId) || !TryGetGroupName(group, out var groupName))
            return;

        if (processedGroups.Contains(groupId))
            return;

        processedGroups.Add(groupId);

        // Build group path
        var groupPath = string.IsNullOrEmpty(parentPath)
            ? groupName
            : $"{parentPath} / {groupName}";

        // === Get all users in this group ===
        var groupMembersResponse = await httpClient.GetAsync($"groups/{groupId}/members", cancellationToken);
        groupMembersResponse.EnsureSuccessStatusCode();

        var groupMembers = await groupMembersResponse.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken) ?? [];

        foreach (var member in groupMembers)
        {
            // Fetch roles for the user
            var rolesResponse = await httpClient.GetAsync($"users/{member.Id}/role-mappings/realm", cancellationToken);
            rolesResponse.EnsureSuccessStatusCode();

            var userRoles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken: cancellationToken) ?? [];

            bool isUserAdmin = userRoles.Any(r =>
                r.Name.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Equals("super-admin", StringComparison.OrdinalIgnoreCase));

            if (!isUserAdmin)
                continue;

            // Initialize dealer if not already added
            if (!result.ContainsKey(member.Id))
            {
                result[member.Id] = new DealerWithGroupsDto
                {
                    DealerId = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    Roles = userRoles.Select(r => r.Name).ToList(),
                    Groups = new List<GroupInfoDto>()
                };
            }

            // Add this group to user's group list if not already added
            if (!result[member.Id].Groups.Any(g => g.GroupId == groupId))
            {
                result[member.Id].Groups.Add(new GroupInfoDto
                {
                    GroupId = groupId,
                    GroupName = groupName,
                    GroupPath = groupPath
                });
            }
        }

        // === Process subGroups recursively ===
        if (group.TryGetValue("subGroups", out var subGroupsObj) &&
            subGroupsObj is JsonElement subGroupsElement &&
            subGroupsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var subGroup in subGroupsElement.EnumerateArray())
            {
                var subGroupDict = JsonSerializer.Deserialize<Dictionary<string, object>>(subGroup.GetRawText(), JsonOptions);
                if (subGroupDict is not null)
                {
                    await CollectAdminsInGroupRecursively(
                        subGroupDict, result, processedGroups, cancellationToken, groupPath
                    );
                }
            }
        }
    }


    private async Task<List<DealerDto>> GetAdminsInGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        SetAuthHeader();

        var response = await httpClient.GetAsync($"groups/{groupId}/members", cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken) ?? [];

        var dealers = new List<DealerDto>();

        foreach (var user in users)
        {
            var rolesResponse = await httpClient.GetAsync($"users/{user.Id}/role-mappings/realm", cancellationToken);
            rolesResponse.EnsureSuccessStatusCode();

            var roles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken: cancellationToken) ?? [];

            bool isDealer = roles.Any(r =>
                string.Equals(r.Name, "admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name, "super-admin", StringComparison.OrdinalIgnoreCase));

            if (isDealer)
            {
                // Fetch group details to get the group name
                var groupResponse = await httpClient.GetAsync($"groups/{groupId}", cancellationToken);
                groupResponse.EnsureSuccessStatusCode();

                var groupDetails = await groupResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: cancellationToken);
                string groupName = groupDetails != null && TryGetGroupName(groupDetails, out var name) ? name : "";

                dealers.Add(new DealerDto
                {
                    DealerId = user.Id,
                    DealerName = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    GroupId = groupId,
                    GroupName = groupName,
                    SubGroupId = null, // Will be set in recursive calls if applicable
                    SubGroupName = null,
                    Roles = roles.Select(r => r.Name).ToList()
                });
            }
        }

        return dealers;
    }

    private bool TryGetGroupId(Dictionary<string, object> group, out string id)
    {
        if (group.TryGetValue("id", out var idObj))
        {
            if (idObj is JsonElement idElem && idElem.ValueKind == JsonValueKind.String)
            {
                id = idElem.GetString()!;
                return true;
            }
            if (idObj is string idStr)
            {
                id = idStr;
                return true;
            }
        }
        id = null!;
        return false;
    }

    private bool TryGetGroupName(Dictionary<string, object> group, out string name)
    {
        if (group.TryGetValue("name", out var nameObj))
        {
            if (nameObj is JsonElement nameElem && nameElem.ValueKind == JsonValueKind.String)
            {
                name = nameElem.GetString()!;
                return true;
            }
            if (nameObj is string nameStr)
            {
                name = nameStr;
                return true;
            }
        }
        name = null!;
        return false;
    }
}