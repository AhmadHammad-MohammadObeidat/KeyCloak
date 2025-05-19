using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.DealersService;
using KeyCloak.Domian;

namespace KeyCloak.Application.Dealers.DeleteDealer;

public sealed class DeleteDealerCommandHandler(
    IDealerManagementService dealerManagementService)
    : ICommandHandler<DeleteDealerCommand, string>
{
    public async Task<Result<string>> Handle(DeleteDealerCommand request, CancellationToken cancellationToken)
    {
        var result = await dealerManagementService.DeleteDealerAsync(request.DealerId, cancellationToken);

        return result.IsFailure
            ? Result.Failure<string>(result.Error)
            : Result.Success(result.Value);
    }
}
