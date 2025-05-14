using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.UsersAccount;
using KeyCloak.Domian;
using Microsoft.Extensions.Logging;

namespace KeyCloak.Application.Users.RegisterAdminUser;

internal sealed class RegisterAdminUserCommandHandler(
IUserAccountService userAccountService,
ILogger<RegisterAdminUserCommandHandler> logger)
: ICommandHandler<RegisterAdminUserCommand, string>
{
    public async Task<Result<string>> Handle(RegisterAdminUserCommand command, CancellationToken cancellationToken)
    {
        var user = new UserModel(
            command.Username,
            command.Email,
            command.Password,
            command.InvestorPassword,
            command.FirstName,
            command.LastName
        );

        return await userAccountService.RegisterAdminUserAsync(user, command.GroupName, cancellationToken);
    }
}
