using KeyCloak.Domian.Dealers;
using KeyCloak.Domian;
using System.Security.Claims;

namespace KeyCloak.Application.Services.DealersService;

public interface IDealerManagementService
{
    Task<List<GroupWithAdminsDto>> GetGroupsWithDealersAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<List<DealerWithGroupsDto>> GetDealersWithGroupsAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<Result<string>> UpdateDealerAsync(string dealerId, string username, string firstName, string lastName, CancellationToken cancellationToken);
    Task<Result<string>> DeleteDealerAsync(string dealerId, CancellationToken cancellationToken);
    Task<Result<string>> ResetPasswordAsync(string dealerId, string newPassword, CancellationToken cancellationToken);
    Task<Result<string>> MoveToGroupAsync(string dealerId, string newGroupId, CancellationToken cancellationToken);
}
