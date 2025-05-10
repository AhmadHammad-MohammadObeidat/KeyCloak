using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Domian;
using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace KeyCloak.Infrastructure.Identity;

public sealed class IdentityProviderService(
    KeyCloakClient keyCloakClient,
    IOptions<KeyCloakOptions> options,
    ILogger<IdentityProviderService> logger) : IIdentityProviderService
{
    private readonly KeyCloakOptions _options = options.Value;

    private const string ApiKeyCredentialType = "api_key";
    private const string PasswordCredentialType = "password";
    private const string InvestorPasswordCredentialType = "investor_password";
    private const string RefreshTokenType = "refresh_token";
    private const string ScopeType = "email openid";

    public async Task<Result<bool>> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            var result = await keyCloakClient.ResendConfirmationEmailAsync(email, _options.AdminUrl, token, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resend confirmation email failed.");
            return Result.Failure<bool>(UsersErrors.InvalidEmail(email));
        }
    }

    public async Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            var result = await keyCloakClient.ForgotPasswordAsync(email, _options.AdminUrl, token, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Forgot password failed.");
            return Result.Failure<bool>(UsersErrors.InvalidEmail(email));
        }
    }

    public async Task<Result<TokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await keyCloakClient.RefreshTokenAsync(
                refreshToken,
                _options.PublicClientId,
                RefreshTokenType,
                _options.TokenUrl,
                cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Token refresh failed.");
            return Result.Failure<TokenResponse>(UsersErrors.InvalidRefreshToken());
        }
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginModel login, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await keyCloakClient.UserLoginAsync(
                login.Username,
                login.Password,
                _options.PublicClientId,
                ScopeType,
                PasswordCredentialType,
                _options.TokenUrl,
                cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "User login failed.");
            return Result.Failure<TokenResponse>(UsersErrors.InvalidUsernameOrPassword());
        }
    }
    public async Task<Result<string>> RegisterUserAsync(UserModel user, string groupName, CancellationToken cancellationToken = default)
    {
        var userRepresentation = new UserRepresentation(
            null,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            EmailVerified: false,
            Enabled: true,
            Credentials: new CredentialRepresentation[]
            {
            new(PasswordCredentialType, user.Password, true),
            new(InvestorPasswordCredentialType, user.InvestorPassword, true),
            new(ApiKeyCredentialType, Guid.NewGuid().ToString("N"), true)
            });

        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);

            var userId = await keyCloakClient.RegisterUserAsync(userRepresentation, token, cancellationToken);

            var groupId = await keyCloakClient.CreateGroupIfNotExistsAsync(groupName, token, cancellationToken);
            await keyCloakClient.AssignUserToGroupAsync(userId, groupId, token, cancellationToken);

            return userId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogError(ex, "User registration failed due to conflict.");
            return Result.Failure<string>(UsersErrors.EmailIsNotUnique(user.Email));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "User registration failed unexpectedly.");
            return Result.Failure<string>(UsersErrors.RegistrationFailed());
        }
    }
    public async Task<Result<string>> RegisterAdminUserAsync(UserModel user, string targetGroup, CancellationToken cancellationToken = default)
    {
        var userRepresentation = new UserRepresentation(
            null,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            EmailVerified: false,
            Enabled: true,
            Credentials: new CredentialRepresentation[]
            {
            new(PasswordCredentialType, user.Password, true),
            new(InvestorPasswordCredentialType, user.InvestorPassword, true),
            new(ApiKeyCredentialType, Guid.NewGuid().ToString("N"), true)
            });

        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);

            // Step 1: Register the admin user
            var userId = await keyCloakClient.RegisterUserAsync(userRepresentation, token, cancellationToken);

            // Step 2: Create group if not exists
            var groupId = await keyCloakClient.CreateGroupIfNotExistsAsync(targetGroup, token, cancellationToken);

            // Step 3: Assign user to group
            await keyCloakClient.AssignUserToGroupAsync(userId, groupId, token, cancellationToken);

            // Step 4: Assign group-admin-* role
            var roleName = $"group-admin-{targetGroup.ToLower()}";
            await keyCloakClient.AssignRealmRoleToUserAsync(userId, roleName, token, cancellationToken);

            return userId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogError(ex, "Admin user registration failed due to conflict.");
            return Result.Failure<string>(UsersErrors.EmailIsNotUnique(user.Email));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin user registration failed unexpectedly.");
            return Result.Failure<string>(UsersErrors.RegistrationFailed());
        }
    }

    public async Task<Result<string>> RegisterDemoUserAsync(UserModel user, CancellationToken cancellationToken = default)
    {
        var userRepresentation = new UserRepresentation(
            null,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            EmailVerified: false,
            Enabled: true,
            Credentials: new CredentialRepresentation[]
            {
                new(PasswordCredentialType, user.Password, true),
                new(InvestorPasswordCredentialType, user.InvestorPassword, true),
                new(ApiKeyCredentialType, Guid.NewGuid().ToString("N"), true)
            });

        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            var identityId = await keyCloakClient.RegisterUserAsync(userRepresentation, token, cancellationToken);
            return identityId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogError(ex, "Demo user registration failed due to conflict.");
            return Result.Failure<string>(UsersErrors.EmailIsNotUnique(user.Email));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo user registration failed unexpectedly.");
            return Result.Failure<string>(UsersErrors.RegistrationFailed());
        }
    }

    public async Task<Result<string>> CreateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default)
    {
        var groupRepresentation = new GroupRepresentation(group.Name, group.ParentId);

        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            // Check if the group already exists
            var existingGroup = await keyCloakClient.GetGroupByNameAsync(group.Name, token, cancellationToken);
            if (existingGroup.Count != 0)
            {
                logger.LogError("Group creation failed: Group with the same name already exists.");
                return Result.Failure<string>(AccountsGroupsErrors.GroupNameIsNotUnique(group.Name));
            }
            var identityId = await keyCloakClient.CreateGroupAsync(groupRepresentation, token, cancellationToken);
            return identityId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogError(ex, "Group creation failed due to conflict.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupNameIsNotUnique(group.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Group creation failed unexpectedly.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupCreationFailed(group.ParentId?.ToString() ?? "null"));
        }
    }

    public async Task<Result<string>> UpdateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default)
    {
        var groupRepresentation = new GroupRepresentation(group.Name, group.ParentId);
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            var identityId = await keyCloakClient.UpdateGroupAsync(groupRepresentation, token, cancellationToken);
            return identityId;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogError(ex, "Group update failed due to conflict.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupNameIsNotUnique(group.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Group update failed unexpectedly.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupUpdateFailed(group.ParentId?.ToString() ?? "null"));
        }

    }
    public async Task<Result<string>> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            var identityId = await keyCloakClient.DeleteGroupAsync(groupId, token, cancellationToken);
            return identityId;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Group Delete failed unexpectedly.");
            return Result.Failure<string>(AccountsGroupsErrors.GroupUpdateFailed(groupId.ToString() ?? "null"));
        }
    }

    private async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await keyCloakClient.GetAdminAccessTokenAsync(
                _options.TokenUrl,
                _options.ClientId,
                _options.ClientSecret,
                _options.AdminUsername,
                _options.AdminPassword,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            var message = $"Failed to get admin token from Keycloak. Status: {ex.StatusCode}";
            logger.LogError(ex, message);
            throw new UnauthorizedAccessException(message, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while getting admin token.");
            throw;
        }
    }
    public async Task<List<UserDto>> GetUsersInCallerGroupAsync(ClaimsPrincipal userPrincipal, CancellationToken cancellationToken)
    {
        var groupClaim = userPrincipal.Claims.FirstOrDefault(c => c.Type == "groups")?.Value;
        if (string.IsNullOrWhiteSpace(groupClaim))
            return new List<UserDto>();

        var token = await GetAdminTokenAsync(cancellationToken);
        return await keyCloakClient.GetUsersByGroupAsync(groupClaim, token, cancellationToken);
    }


}
