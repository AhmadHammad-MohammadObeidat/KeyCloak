using KeyCloak.Application.Messaging;
using KeyCloak.Domian;
using MediatR;
using KeyCloak.Application.Services.GroupsService;

namespace KeyCloak.Application.Groups.GetAllGroups;

internal sealed class GetAllGroupsQueryHandler(
IUserGroupQueryService userGroupQueryService)
: IQueryHandler<GetAllGroupsQuery, Result<List<Dictionary<string, object>>>>,
  IRequestHandler<GetAllGroupsQuery, Result<List<Dictionary<string, object>>>>
{
    public async Task<Result<List<Dictionary<string, object>>>> Handle(GetAllGroupsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var allGroups = await userGroupQueryService.GetAllGroupsAsync(cancellationToken);
            return Result.Success(allGroups);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<Dictionary<string, object>>>(
                new Error("An error occurred while fetching groups.", "Get all groups failed", ErrorType.Problem));
        }
    }
}
