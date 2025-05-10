using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using System.Security.Claims;
using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Users.GetUsersByGroup;

public sealed class GetUsersByGroupQuery : IQuery<Result<List<User>>>
{
    public GetUsersByGroupQuery(ClaimsPrincipal userPrincipal)
    {
        UserPrincipal = userPrincipal;
    }

    public ClaimsPrincipal UserPrincipal { get; }
}
