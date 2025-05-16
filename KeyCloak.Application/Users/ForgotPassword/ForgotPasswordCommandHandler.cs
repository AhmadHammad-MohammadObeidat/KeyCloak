using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.UsersEmailService;
using KeyCloak.Domian;

namespace KeyCloak.Application.Users.ForgotPassword;
public sealed class ForgotPasswordCommandHandler(
    IUserEmailService userEmailService)
    : ICommandHandler<ForgotPasswordCommand, bool>
{
    public async Task<Result<bool>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        Result<bool> result = await userEmailService.ForgotPasswordAsync(
            request.Email,
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        return result.Value;
    }
}
