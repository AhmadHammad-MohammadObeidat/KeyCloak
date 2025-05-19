using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using KeyCloak.Application.Services.UsersAccount;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakAuthClients;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakGroupClients;

namespace KeyCloak.Infrastructure.Identity.Services.UsersAccount;

public sealed class UserAccountService(KeycloakAuthClient _keycloakAuthClient, KeycloakGroupClient _keycloakGroupClient, IOptions<KeyCloakOptions> _options,
    ILogger<UserAccountService> _logger) : IUserAccountService
{
    public async Task<Result<TokenResponse>> LoginAsync(LoginModel login, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _keycloakAuthClient.UserLoginAsync(
                login.Username,
                login.Password,
                _options.Value.PublicClientId,
                "email openid",
                "password",
                _options.Value.TokenUrl,
                cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "User login failed.");
            return Result.Failure<TokenResponse>(UsersErrors.InvalidUsernameOrPassword());
        }
    }

    public async Task<Result<TokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _keycloakAuthClient.RefreshTokenAsync(
                refreshToken,
                _options.Value.PublicClientId,
                "refresh_token",
                _options.Value.TokenUrl,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed.");
            return Result.Failure<TokenResponse>(UsersErrors.InvalidRefreshToken());
        }
    }

    public async Task<Result<string>> RegisterUserAsync(UserModel user, string groupName, CancellationToken cancellationToken = default)
    {
        var userRepresentation = CreateUserRepresentation(user);

        try
        {
            var userId = await _keycloakAuthClient.RegisterUserAsync(userRepresentation, cancellationToken);
            var groupId = await _keycloakGroupClient.CreateGroupIfNotExistsAsync(groupName, cancellationToken);
            await _keycloakGroupClient.AssignUserToGroupAsync(userId, groupId, cancellationToken);
            return userId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogError(ex, "User registration conflict.");
            return Result.Failure<string>(UsersErrors.EmailIsNotUnique(user.Email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected registration failure.");
            return Result.Failure<string>(UsersErrors.RegistrationFailed());
        }
    }

    public async Task<Result<string>> RegisterAdminUserAsync(UserModel user, string targetGroup, CancellationToken cancellationToken = default)
    {
        var userRepresentation = CreateUserRepresentation(user);

        try
        {
            var userId = await _keycloakAuthClient.RegisterUserAsync(userRepresentation, cancellationToken);
            var groupId = await _keycloakGroupClient.CreateGroupIfNotExistsAsync(targetGroup, cancellationToken);
            await _keycloakGroupClient.AssignUserToGroupAsync(userId, groupId, cancellationToken);
            await _keycloakGroupClient.AssignRealmRoleToUserAsync(userId, $"group-admin-{targetGroup.ToLower()}", cancellationToken);
            return userId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogError(ex, "Admin registration conflict.");
            return Result.Failure<string>(UsersErrors.EmailIsNotUnique(user.Email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected admin registration failure.");
            return Result.Failure<string>(UsersErrors.RegistrationFailed());
        }
    }

    public async Task<Result<string>> RegisterDemoUserAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        var userRepresentation = CreateUserRepresentation(user);

        try
        {
            return await _keycloakAuthClient.RegisterUserAsync(userRepresentation, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogError(ex, "Demo user registration conflict.");
            return Result.Failure<string>(UsersErrors.EmailIsNotUnique(user.Email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected demo user registration failure.");
            return Result.Failure<string>(UsersErrors.RegistrationFailed());
        }
    }

    private static UserRepresentation CreateUserRepresentation(UserModel user)
    {
        return new UserRepresentation(
            null,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            EmailVerified: false,
            Enabled: true,
            Credentials: new[]
            {
                new CredentialRepresentation("password", user.Password, true),
                new CredentialRepresentation("investor_password", user.InvestorPassword, true),
                new CredentialRepresentation("api_key", Guid.NewGuid().ToString("N"), true)
            });
    }
}
