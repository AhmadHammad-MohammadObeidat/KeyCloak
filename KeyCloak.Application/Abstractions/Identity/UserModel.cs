namespace KeyCloak.Application.Abstractions.Identity;

public sealed record UserModel(string UserName, string Email, string Password, string InvestorPassword, string FirstName, string LastName);

