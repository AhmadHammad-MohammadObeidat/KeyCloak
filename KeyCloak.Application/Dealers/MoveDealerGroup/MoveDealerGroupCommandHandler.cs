using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.DealersService;
using KeyCloak.Domian;

namespace KeyCloak.Application.Dealers.MoveDealerGroup;

public sealed class MoveDealerGroupCommandHandler(
    IDealerManagementService dealerManagementService)
    : ICommandHandler<MoveDealerGroupCommand, string>
{
    public async Task<Result<string>> Handle(MoveDealerGroupCommand request, CancellationToken cancellationToken)
    {
        var result = await dealerManagementService.MoveToGroupAsync(request.DealerId, request.NewGroupId, cancellationToken);

        return result.IsFailure
            ? Result.Failure<string>(result.Error)
            : Result.Success(result.Value);
    }
}
