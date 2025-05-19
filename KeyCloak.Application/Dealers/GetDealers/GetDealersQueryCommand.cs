using KeyCloak.Application.Messaging;
using KeyCloak.Domian.Dealers;
using MediatR;
using System.Security.Claims;

namespace KeyCloak.Application.Dealers.GetDealers;


public record GetDealersQueryCommand(ClaimsPrincipal User) : IQuery<List<DealerWithGroupsDto>>;

