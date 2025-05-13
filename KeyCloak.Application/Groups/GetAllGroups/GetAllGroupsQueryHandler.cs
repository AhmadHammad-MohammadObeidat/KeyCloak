using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Users.GetUsersByGroup;
using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using MediatR;
using System.Security.Claims;

namespace KeyCloak.Application.Groups.GetAllGroups;

internal sealed class GetAllGroupsQueryHandler(
IIdentityProviderService identityProviderService)
: IQueryHandler<GetAllGroupsQuery, Result<List<Dictionary<string, object>>>>,
  IRequestHandler<GetAllGroupsQuery, Result<List<Dictionary<string, object>>>>
{
    public async Task<Result<List<Dictionary<string, object>>>> Handle(GetAllGroupsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var allGroups = await identityProviderService.GetAllGroupsAsync(cancellationToken);
            return Result.Success(allGroups);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<Dictionary<string, object>>>(
                new Error("An error occurred while fetching groups.", "Get all groups failed", ErrorType.Problem));
        }
    }
}
