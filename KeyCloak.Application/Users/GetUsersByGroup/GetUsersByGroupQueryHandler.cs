using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Users.GetUsersByGroup;

internal sealed class GetUsersByGroupQueryHandler(
IIdentityProviderService identityProviderService)
: IQueryHandler<GetUsersByGroupQuery, Result<List<User>>>
{
    public async Task<Result<List<User>>> Handle(GetUsersByGroupQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var userDtos = await identityProviderService.GetUsersInCallerGroupAsync(query.UserPrincipal, cancellationToken);
            var users = userDtos.Select(User.FromDto).ToList();
            return Result.Success(users);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<User>>(UsersErrors.FailedToRetrieveUsers());
        }
    }
}
