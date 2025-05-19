using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian;
using Microsoft.Extensions.Logging;
using System.Net;
using KeyCloak.Application.Services.GroupsService;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakGroupClients;

namespace KeyCloak.Infrastructure.Identity.Services.GroupsService;

public sealed class GroupManagementService(KeycloakGroupClient _keyCloakGroupClient, ILogger<GroupManagementService> _logger) : IGroupManagementService
{
    public async Task<Result<string>> CreateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingGroup = await _keyCloakGroupClient.GetGroupByNameAsync(group.Name, cancellationToken);
            if (existingGroup.Count != 0)
            {
                return Result.Failure<string>(AccountsGroupsErrors.GroupNameIsNotUnique(group.Name));
            }
            return await _keyCloakGroupClient.CreateGroupAsync(group, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogError(ex, "Group creation conflict.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupNameIsNotUnique(group.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected group creation failure.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupCreationFailed(group.ParentId?.ToString() ?? "null"));
        }
    }

    public async Task<Result<string>> UpdateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _keyCloakGroupClient.UpdateGroupAsync(group, cancellationToken))
            {
                throw new HttpRequestException("Group update conflict.", null, HttpStatusCode.Conflict);
            }
            return Result.Success(group.GroupId.ToString());
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogError(ex, "Group update conflict.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupNameIsNotUnique(group.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected group update failure.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupUpdateFailed(group.ParentId?.ToString() ?? "null"));
        }
    }

    public async Task<Result<string>> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _keyCloakGroupClient.DeleteGroupAsync(groupId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected group deletion failure.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupUpdateFailed(groupId.ToString()));
        }
    }
}
