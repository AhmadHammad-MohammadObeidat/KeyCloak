using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Domian;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Users.RegisterAdminUser
{
    internal sealed class RegisterAdminUserCommandHandler(
    IIdentityProviderService identityService,
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

            return await identityService.RegisterAdminUserAsync(user, command.GroupName, cancellationToken);
        }
    }
}
