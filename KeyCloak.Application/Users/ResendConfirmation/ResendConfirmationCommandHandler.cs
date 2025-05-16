using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.UsersEmailService;
using KeyCloak.Domian;
namespace KeyCloak.Application.Users.ResendConfirmation;

public sealed class ResendConfirmationCommandHandler(
    IUserEmailService userEmailService)
    : ICommandHandler<ResendConfirmationCommand, bool>
{
    public async Task<Result<bool>> Handle(ResendConfirmationCommand request, CancellationToken cancellationToken)
    {
        Result<bool> result = await userEmailService.ResendConfirmationEmailAsync(
            request.Email,
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        return result.Value;
    }
}
