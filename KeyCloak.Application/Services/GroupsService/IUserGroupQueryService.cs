using KeyCloak.Application.Groups.GetGroupWithUsers;
using KeyCloak.Domian.Users;
using System.Security.Claims;

namespace KeyCloak.Application.Services.GroupsService;

public interface IUserGroupQueryService
{
    Task<List<UserDto>> GetUsersInCallerGroupAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object>>> GetAllGroupsAsync(CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, object>>> GetFilteredGroupsAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<List<GroupWithUsersDto>> GetGroupsWithUsersByRolesAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}
