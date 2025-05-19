using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.GroupsService;

namespace KeyCloak.Application.Users.GetUsersByGroup;

public class GetUsersByGroupQueryHandler(IUserGroupQueryService userGroupQueryService) : IQueryHandler<GetUsersByGroupQuery, Result<List<UserDto>>>
{

    public async Task<Result<List<UserDto>>> Handle(GetUsersByGroupQuery request, CancellationToken cancellationToken)
    {
        var users = await userGroupQueryService.GetUsersInCallerGroupAsync(request.User, cancellationToken);
        return Result.Success(users); // users must already be List<UserDto>
    }
}
