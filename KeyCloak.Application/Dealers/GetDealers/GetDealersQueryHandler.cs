using KeyCloak.Application.Messaging;
using KeyCloak.Application.Services.DealersService;
using KeyCloak.Domian;
using KeyCloak.Domian.Dealers;

namespace KeyCloak.Application.Dealers.GetDealers;

public class GetDealersQueryCommandHandler(IDealerManagementService dealerManagementService)
    : IQueryHandler<GetDealersQueryCommand, List<DealerWithGroupsDto>>
{
    public async Task<List<DealerWithGroupsDto>> Handle(GetDealersQueryCommand request, CancellationToken cancellationToken)
    {
        return await dealerManagementService.GetDealersWithGroupsAsync(request.User, cancellationToken);
    }
}