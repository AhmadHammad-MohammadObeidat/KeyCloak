using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Domian;
namespace KeyCloak.Application.Users.ResendConfirmation;

public sealed class ResendConfirmationCommandHandler(
    IIdentityProviderService identityProviderService)
    : ICommandHandler<ResendConfirmationCommand, bool>
{
    public async Task<Result<bool>> Handle(ResendConfirmationCommand request, CancellationToken cancellationToken)
    {
        Result<bool> result = await identityProviderService.ResendConfirmationEmailAsync(
            request.Email,
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        return result.Value;
    }
}
