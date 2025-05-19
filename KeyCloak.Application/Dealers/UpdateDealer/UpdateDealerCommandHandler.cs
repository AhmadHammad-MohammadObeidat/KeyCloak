using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.DealersService;
using KeyCloak.Domian;

namespace KeyCloak.Application.Dealers.UpdateDealer;

public sealed class UpdateDealerCommandHandler(
    IDealerManagementService dealerManagementService)
    : ICommandHandler<UpdateDealerCommand, string>
{
    public async Task<Result<string>> Handle(UpdateDealerCommand request, CancellationToken cancellationToken)
    {
        var result = await dealerManagementService.UpdateDealerAsync(
            request.DealerId,
            request.Username,
            request.FirstName,
            request.LastName,
            cancellationToken);

        return result.IsFailure
            ? Result.Failure<string>(result.Error)
            : Result.Success(result.Value);
    }
}
