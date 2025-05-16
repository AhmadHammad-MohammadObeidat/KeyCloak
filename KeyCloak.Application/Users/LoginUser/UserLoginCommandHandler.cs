using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.UsersAccount;
using KeyCloak.Domian;

namespace KeyCloak.Application.Users.LoginUser;

public sealed class UserLoginCommandHandler(
   IUserAccountService userAccountService)
    : ICommandHandler<UserLoginCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> Handle(UserLoginCommand request, CancellationToken cancellationToken)
    {
        Result<TokenResponse> result = await userAccountService.LoginAsync(
            new LoginModel(request.Username, request.Password),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<TokenResponse>(result.Error);
        }

        return result.Value;
    }
}
