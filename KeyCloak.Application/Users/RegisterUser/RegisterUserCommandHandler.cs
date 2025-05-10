using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Users.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IIdentityProviderService identityProviderService)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var user = new UserModel(
            command.Username,
            command.Email,
            command.Password,
            command.InvestorPassword,
            command.FirstName,
            command.LastName
        );

        var result = await identityProviderService.RegisterUserAsync(user, command.GroupName, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        return Result.Success(Guid.Parse(result.Value));
    }
}