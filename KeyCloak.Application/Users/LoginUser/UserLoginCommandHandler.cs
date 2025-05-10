using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Domian;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Users.LoginUser;

public sealed class UserLoginCommandHandler(
    IIdentityProviderService identityProviderService)
    : ICommandHandler<UserLoginCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> Handle(UserLoginCommand request, CancellationToken cancellationToken)
    {
        Result<TokenResponse> result = await identityProviderService.LoginAsync(
            new LoginModel(request.Username, request.Password),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<TokenResponse>(result.Error);
        }

        return result.Value;
    }
}
