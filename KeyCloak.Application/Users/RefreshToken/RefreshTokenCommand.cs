using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Users.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<TokenResponse>;

