using MediatR;
using System.Security.Claims;

namespace KeyCloak.Application.Groups.GetGroupWithUsers;

public record GetGroupsWithUsersQuery(ClaimsPrincipal User) : IRequest<List<GroupWithUsersDto>>;

