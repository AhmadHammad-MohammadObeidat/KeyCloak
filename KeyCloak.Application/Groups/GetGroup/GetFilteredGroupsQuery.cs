using MediatR;
using System.Security.Claims;

namespace KeyCloak.Application.Groups.GetGroup;

public sealed record GetFilteredGroupsQuery(ClaimsPrincipal User) : IRequest<List<Dictionary<string, object>>>;
