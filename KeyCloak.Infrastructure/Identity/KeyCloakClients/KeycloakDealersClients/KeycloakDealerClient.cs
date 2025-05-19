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

    public async Task<List<DealerWithGroupsDto>> GetDealersWithGroupsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var result = new Dictionary<string, DealerWithGroupsDto>();

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
            return [];

        var isSuperAdmin = roles.Contains("super-admin");
        var isAdmin = roles.Contains("admin") || isSuperAdmin;

        if (!isAdmin)
            return [];

        // Get user's groups
        var userGroupsResponse = await httpClient.GetAsync($"users/{userId}/groups", cancellationToken);
        userGroupsResponse.EnsureSuccessStatusCode();
        var userGroups = await userGroupsResponse.Content.ReadFromJsonAsync<List<KeycloakGroup>>(cancellationToken: cancellationToken) ?? [];

        // Case 1: Super-admin with no groups - return all admins with their groups
        if (isSuperAdmin && !userGroups.Any())
        {
            // Get all users
            var usersResponse = await httpClient.GetAsync("users", cancellationToken);
            usersResponse.EnsureSuccessStatusCode();
            var allUsers = await usersResponse.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken) ?? [];

            foreach (var userRecord in allUsers)
            {
                // Check if user is admin or super-admin
                var rolesResponse = await httpClient.GetAsync($"users/{userRecord.Id}/role-mappings/realm", cancellationToken);
                rolesResponse.EnsureSuccessStatusCode();
                var userRoles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken: cancellationToken) ?? [];

                bool isUserAdmin = userRoles.Any(r =>
                    r.Name.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                    r.Name.Equals("super-admin", StringComparison.OrdinalIgnoreCase));

                if (!isUserAdmin)
                    continue;

                // Get admin's groups
                var adminGroupsResponse = await httpClient.GetAsync($"users/{userRecord.Id}/groups", cancellationToken);
                adminGroupsResponse.EnsureSuccessStatusCode();
                var adminGroups = await adminGroupsResponse.Content.ReadFromJsonAsync<List<KeycloakGroup>>(cancellationToken: cancellationToken) ?? [];

                if (!result.ContainsKey(userRecord.Id))
                {
                    result[userRecord.Id] = new DealerWithGroupsDto
                    {
                        DealerId = userRecord.Id,
                        FirstName = userRecord.FirstName,
                        LastName = userRecord.LastName,
                        Email = userRecord.Email,
                        Roles = userRoles.Select(r => r.Name).ToList(),
                        Groups = new List<GroupInfoDto>()
                    };
                }

                // Add groups to admin
                foreach (var group in adminGroups)
                {
                    result[userRecord.Id].Groups.Add(new GroupInfoDto
                    {
                        GroupId = group.Id,
                        GroupName = group.Name
                    });
                }
            }
        }
        // Case 2: Admin with groups - return admins in those groups with their groups and subgroups
        else if (userGroups.Any())
        {
            var processedGroups = new HashSet<string>();

            foreach (var group in userGroups)
            {
                // Get detailed group information including subgroups
                var groupDetailsResponse = await httpClient.GetAsync($"groups/{group.Id}", cancellationToken);
                groupDetailsResponse.EnsureSuccessStatusCode();

                var groupDetails = await groupDetailsResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>(JsonOptions, cancellationToken);
                if (groupDetails is not null)
                {
                    await CollectAdminsInGroupRecursively(groupDetails, result, processedGroups, cancellationToken);
                }
            }
        }

        return result.Values.ToList();
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
        CancellationToken cancellationToken)
    {
        if (!TryGetGroupId(group, out var groupId) || !TryGetGroupName(group, out var groupName))
            return;

        // Skip if we've already processed this group
        if (processedGroups.Contains(groupId))
            return;

        processedGroups.Add(groupId);

        // Get all members in the group
        var groupMembersResponse = await httpClient.GetAsync($"groups/{groupId}/members", cancellationToken);
        groupMembersResponse.EnsureSuccessStatusCode();
        var groupMembers = await groupMembersResponse.Content.ReadFromJsonAsync<List<UserRepresentation>>(cancellationToken: cancellationToken) ?? [];

        foreach (var member in groupMembers)
        {
            // Check if member is admin or super-admin
            var rolesResponse = await httpClient.GetAsync($"users/{member.Id}/role-mappings/realm", cancellationToken);
            rolesResponse.EnsureSuccessStatusCode();
            var userRoles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleRepresentation>>(cancellationToken: cancellationToken) ?? [];

            bool isUserAdmin = userRoles.Any(r =>
                r.Name.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Equals("super-admin", StringComparison.OrdinalIgnoreCase));

            if (!isUserAdmin)
                continue;

            // Add admin if not already in result
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

            // Add current group to admin's groups if not already there
            if (!result[member.Id].Groups.Any(g => g.GroupId == groupId))
            {
                result[member.Id].Groups.Add(new GroupInfoDto
                {
                    GroupId = groupId,
                    GroupName = groupName
                });
            }
        }

        // Process subgroups recursively
        if (group.TryGetValue("subGroups", out var subGroupsObj) &&
            subGroupsObj is JsonElement subGroupsElement &&
            subGroupsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var subGroup in subGroupsElement.EnumerateArray())
            {
                var subGroupDict = JsonSerializer.Deserialize<Dictionary<string, object>>(subGroup.GetRawText(), JsonOptions);
                if (subGroupDict is not null)
                {
                    await CollectAdminsInGroupRecursively(subGroupDict, result, processedGroups, cancellationToken);
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

            bool isDealers = roles.Any(r =>
                string.Equals(r.Name, "admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name, "super-admin", StringComparison.OrdinalIgnoreCase));

            if (isDealers)
            {
                dealers.Add(new DealerDto
                {
                    DealerId = user.Id,
                    DealerName = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    GroupId = "",
                    GroupName = "",
                    SubGroupId = null,
                    SubGroupName = null,
                    Roles = roles.Select(r => r.Name).ToList()
                });
            }
        }

        return dealers;
    }

    private static bool TryGetGroupId(Dictionary<string, object> group, out string groupId)
    {
        groupId = string.Empty;

        if (group.TryGetValue("id", out var idValue))
        {
            groupId = idValue switch
            {
                JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString() ?? "",
                string str => str,
                _ => ""
            };

            return !string.IsNullOrWhiteSpace(groupId);
        }

        return false;
    }

    private static bool TryGetGroupName(Dictionary<string, object> group, out string groupName)
    {
        groupName = string.Empty;

        if (group.TryGetValue("name", out var nameValue))
        {
            groupName = nameValue switch
            {
                JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString() ?? "",
                string str => str,
                _ => ""
            };

            return !string.IsNullOrWhiteSpace(groupName);
        }

        return false;
    }
}