namespace KeyCloak.Api.Controllers.Users;

public sealed record RegisterUserRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string InvestorPassword,
    string GroupName);

