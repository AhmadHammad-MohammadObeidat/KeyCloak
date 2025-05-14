using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.GroupsService;
using KeyCloak.Domian;
using KeyCloak.Domian.AccountsGroups;

namespace KeyCloak.Application.Groups.CreateGroup;

public sealed class CreateGroupCommandHandler(
    IGroupManagementService groupManagementService)
    : ICommandHandler<CreateGroupCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        var result = await groupManagementService.CreateGroupAsync(new GroupRepresentation(request.GroupName, null), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }
        return Result.Success(Guid.Parse(result.Value));
    }
}
