using KeyCloak.Domian.Users;
using KeyCloak.Domian;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KeyCloak.Application.Services.UsersEmailService;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakAuthClients;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakUserClients;

namespace KeyCloak.Infrastructure.Identity.Services.UsersEmailService;

public sealed class UserEmailService(KeycloakUserClient _keycloakUserClient, IOptions<KeyCloakOptions> _options, ILogger<UserEmailService> _logger) : IUserEmailService
{
    public async Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _keycloakUserClient.ForgotPasswordAsync(email, _options.Value.AdminUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password failed.");
            return Result.Failure<bool>(UsersErrors.InvalidEmail(email));
        }
    }

    public async Task<Result<bool>> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _keycloakUserClient.ResendConfirmationEmailAsync(email, _options.Value.AdminUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend confirmation email failed.");
            return Result.Failure<bool>(UsersErrors.InvalidEmail(email));
        }
    }
}
