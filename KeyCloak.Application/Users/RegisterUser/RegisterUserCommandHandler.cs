using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian;
using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.UsersAccount;

namespace KeyCloak.Application.Users.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserAccountService userAccountService)
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

        var result = await userAccountService.RegisterUserAsync(user, command.GroupName, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        return Result.Success(Guid.Parse(result.Value));
    }
}