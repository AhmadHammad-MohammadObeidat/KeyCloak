namespace KeyCloak.Api.Controllers.Users;

public sealed record UserResponse(Guid UserId, string IdentityId, string Email, string FirstName,
                                string LastName, string Password);
