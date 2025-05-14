using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.GroupsService;
using KeyCloak.Domian;
using KeyCloak.Domian.AccountsGroups;

namespace KeyCloak.Application.Groups.UpdateGroup;

public sealed class UpdateGroupCommandHandler(
   IGroupManagementService groupManagementService)
    : ICommandHandler<UpdateGroupCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        var result = await groupManagementService.UpdateGroupAsync(new GroupRepresentation(request.GroupName, request.GroupId, null), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }
        return Result.Success(Guid.Parse(result.Value));
    }
}
