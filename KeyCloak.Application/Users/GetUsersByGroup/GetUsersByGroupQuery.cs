using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using System.Security.Claims;
using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Users.GetUsersByGroup;

public sealed record GetUsersByGroupQuery(ClaimsPrincipal User) : IQuery<Result<List<UserDto>>>;

