using KeyCloak.Domian;
using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian.Users;
using System.Security.Claims;

namespace KeyCloak.Application.Abstractions.Identity;

public interface IIdentityProviderService
{
    Task<Result<TokenResponse>> LoginAsync(LoginModel login, CancellationToken cancellationToken = default);
    Task<Result<TokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterUserAsync(UserModel user, string groupName, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterDemoUserAsync(UserModel user, CancellationToken cancellationToken = default);
    Task<Result<string>> CreateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default);
    Task<Result<string>> UpdateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default);
    Task<Result<string>> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<bool>> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterAdminUserAsync(UserModel user, string targetGroup, CancellationToken cancellationToken = default);
    Task<List<UserDto>> GetUsersInCallerGroupAsync(ClaimsPrincipal userPrincipal, CancellationToken cancellationToken);
}
