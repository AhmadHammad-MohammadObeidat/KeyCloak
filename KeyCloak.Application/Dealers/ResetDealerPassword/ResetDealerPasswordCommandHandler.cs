using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.DealersService;
using KeyCloak.Domian;

namespace KeyCloak.Application.Dealers.ResetDealerPassword;


public sealed class ResetDealerPasswordCommandHandler(
    IDealerManagementService dealerManagementService)
    : ICommandHandler<ResetDealerPasswordCommand, string>
{
    public async Task<Result<string>> Handle(ResetDealerPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await dealerManagementService.ResetPasswordAsync(request.DealerId, request.NewPassword, cancellationToken);

        return result.IsFailure
            ? Result.Failure<string>(result.Error)
            : Result.Success(result.Value);
    }
}