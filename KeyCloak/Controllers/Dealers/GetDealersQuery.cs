using KeyCloak.Domian.Dealers;
using MediatR;
using System.Security.Claims;

namespace KeyCloak.Api.Controllers.Dealers;

public sealed record GetDealersQuery(ClaimsPrincipal User, string? GroupName) : IRequest<List<DealerDto>>;
