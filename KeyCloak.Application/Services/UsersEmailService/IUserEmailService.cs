using KeyCloak.Domian;

namespace KeyCloak.Application.Services.UsersEmailService;

public interface IUserEmailService
{
    Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<bool>> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default);
}
