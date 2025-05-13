using KeyCloak.Application.Abstractions.Identity;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Groups.GetGroupWithUsers;

public class GetGroupsWithUsersQueryHandler(IIdentityProviderService identityProviderService) : IRequestHandler<GetGroupsWithUsersQuery, List<GroupWithUsersDto>>
{
    public async Task<List<GroupWithUsersDto>> Handle(GetGroupsWithUsersQuery request, CancellationToken cancellationToken)
    {
        return await identityProviderService.GetGroupsWithUsersByRolesAsync(request.User, cancellationToken);
    }
}

