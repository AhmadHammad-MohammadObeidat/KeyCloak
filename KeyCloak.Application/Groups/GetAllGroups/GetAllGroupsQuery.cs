using KeyCloak.Application.Messaging;
using KeyCloak.Domian;
using MediatR;
namespace KeyCloak.Application.Groups.GetAllGroups;

public sealed record GetAllGroupsQuery() : IQuery<Result<List<Dictionary<string, object>>>>, IRequest<Result<List<Dictionary<string, object>>>>;