using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.GroupsService;
using KeyCloak.Domian;

namespace KeyCloak.Application.Groups.DeleteGroup;

public sealed class DeleteGroupCommandHandler(IGroupManagementService groupManagementService)
    : ICommandHandler<DeleteGroupCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var result = await groupManagementService.DeleteGroupAsync(request.GroupId, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }
        return Result.Success(Guid.Parse(result.Value));
    }
}
