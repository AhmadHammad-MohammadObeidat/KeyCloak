namespace KeyCloak.Api.Controllers.Users;

public class RegisterAdminUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string InvestorPassword { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
}
