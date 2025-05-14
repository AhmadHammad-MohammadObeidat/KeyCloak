using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.GroupsService;

namespace KeyCloak.Application.Users.GetUsersByGroup;

internal sealed class GetUsersByGroupQueryHandler(
IUserGroupQueryService userGroupQueryService)
: IQueryHandler<GetUsersByGroupQuery, Result<List<User>>>
{
    public async Task<Result<List<User>>> Handle(GetUsersByGroupQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var userDtos = await userGroupQueryService.GetUsersInCallerGroupAsync(query.UserPrincipal, cancellationToken);
            var users = userDtos.Select(User.FromDto).ToList();
            return Result.Success(users);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<User>>(UsersErrors.FailedToRetrieveUsers());
        }
    }
}
