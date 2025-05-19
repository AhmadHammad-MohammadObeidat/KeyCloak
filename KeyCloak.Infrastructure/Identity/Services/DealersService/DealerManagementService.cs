using KeyCloak.Application.Services.DealersService;
using KeyCloak.Application.Services.RolesExtractionService;
using KeyCloak.Domian.Dealers;
using KeyCloak.Domian;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakDealersClients;

namespace KeyCloak.Infrastructure.Identity.Services.DealersService;

public sealed class DealerManagementService(
    KeycloakDealerClient keycloakDealerClient,
    IRoleExtractionService roleExtractor,
    ILogger<DealerManagementService> logger
) : IDealerManagementService
{
    public async Task<Result<string>> UpdateDealerAsync(string dealerId, string username, string firstName, string lastName, CancellationToken cancellationToken)
    {
        var result = await keycloakDealerClient.UpdateUserAsync(dealerId, username, firstName, lastName, cancellationToken);
        return result.IsFailure ? Result.Failure<string>(result.Error) : Result.Success("Dealer updated successfully");
    }

    public async Task<Result<string>> DeleteDealerAsync(string dealerId, CancellationToken cancellationToken)
    {
        var result = await keycloakDealerClient.DeleteUserAsync(dealerId, cancellationToken);
        return result.IsFailure ? Result.Failure<string>(result.Error) : Result.Success("Dealer deleted successfully");
    }

    public async Task<Result<string>> ResetPasswordAsync(string dealerId, string newPassword, CancellationToken cancellationToken)
    {
        var result = await keycloakDealerClient.ResetPasswordAsync(dealerId, newPassword, cancellationToken);
        return result.IsFailure ? Result.Failure<string>(result.Error) : Result.Success("Password reset successfully");
    }

    public async Task<Result<string>> MoveToGroupAsync(string dealerId, string newGroupId, CancellationToken cancellationToken)
    {
        var result = await keycloakDealerClient.MoveUserToGroupAsync(dealerId, newGroupId, cancellationToken);
        return result.IsFailure ? Result.Failure<string>(result.Error) : Result.Success("Dealer moved to new group");
    }
    public async Task<List<GroupWithAdminsDto>> GetGroupsWithDealersAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return await keycloakDealerClient.GetGroupsWithAdminsAsync(user, cancellationToken);
    }
    public async Task<List<DealerWithGroupsDto>> GetDealersWithGroupsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return await keycloakDealerClient.GetDealersWithGroupsAsync(user, cancellationToken);
    }
}

