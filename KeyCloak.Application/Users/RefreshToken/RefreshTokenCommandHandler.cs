using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.UsersAccount;
using KeyCloak.Domian;

namespace KeyCloak.Application.Users.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IUserAccountService userAccountService)
    : ICommandHandler<RefreshTokenCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        Result<TokenResponse> result = await userAccountService.RefreshTokenAsync(
            request.RefreshToken,
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<TokenResponse>(result.Error);
        }

        return result.Value;
    }
}
