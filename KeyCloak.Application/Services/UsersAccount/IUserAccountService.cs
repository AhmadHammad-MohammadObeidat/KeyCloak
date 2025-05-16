using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian;

namespace KeyCloak.Application.Services.UsersAccount;

public interface IUserAccountService
{
    Task<Result<TokenResponse>> LoginAsync(LoginModel login, CancellationToken cancellationToken = default);
    Task<Result<TokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterUserAsync(UserModel user, string groupName, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterAdminUserAsync(UserModel user, string targetGroup, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterDemoUserAsync(UserModel user, CancellationToken cancellationToken = default);
}
