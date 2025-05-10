using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;
using KeyCloak.Domian;
using KeyCloak.Domian.AccountsGroups;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Groups.CreateGroup;

public sealed class CreateGroupCommandHandler(
    IIdentityProviderService identityProviderService)
    : ICommandHandler<CreateGroupCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
       var result = await identityProviderService.CreateGroupAsync(new GroupRepresentation(request.GroupName, null), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }
        return Result.Success(Guid.Parse(result.Value));
    }
}
