using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;


namespace KeyCloak.Application.Users.LoginUser;

public sealed record UserLoginCommand(string Username, string Password) : ICommand<TokenResponse>;

