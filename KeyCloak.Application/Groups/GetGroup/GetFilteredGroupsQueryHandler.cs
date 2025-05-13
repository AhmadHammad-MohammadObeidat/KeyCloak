using KeyCloak.Application.Abstractions.Identity;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Groups.GetGroup;

internal sealed class GetFilteredGroupsQueryHandler(IIdentityProviderService identityProviderService) : IRequestHandler<GetFilteredGroupsQuery, List<Dictionary<string, object>>>
{

    public async Task<List<Dictionary<string, object>>> Handle(GetFilteredGroupsQuery request, CancellationToken cancellationToken)
    {
        return await identityProviderService.GetFilteredGroupsAsync(request.User, cancellationToken);
    }
}