using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Domian.Users;

public static class UsersErrors
{
    public static Error InvalidUsernameOrPassword() =>
            Error.NotFound("Users.InvalidUsernameOrPassword", $"Invalid username or password.");
    public static Error EmailIsNotUnique(string email) =>
            Error.NotFound("Users.EmailIsNotUnique", $"The specific {email} email is not unique.");
    public static Error InvalidEmail(string email) =>
            Error.NotFound("Users.InvalidEmail", $"The specific {email} email is not vaild.");
    public static Error NotFound(Guid userId) =>
            Error.NotFound("Users.UserNotFound", $"The user with the identifier {userId} was not found");
    public static Error PermissionsNotFound(string identityId) =>
            Error.NotFound("Users.PermissionsNotFound", $"There is no permissions for the user with the identity {identityId}.");
    public static Error InvalidRefreshToken() =>
            Error.NotFound("Users.InvalidRefreshToken", $"Invalid refresh token.");
    public static Error InvalidUserIdentifier(string value) =>
            Error.NotFound("Users.InvalidUserIdentifier", $"Invalid User Identifier {value}.");
   public static Error RegistrationFailed() =>
            Error.NotFound("Users.RegistrationFailed", $"Registration Failed.");
    public static Error FailedToRetrieveUsers() =>
            Error.Failure("Users.FailedToRetrieve", "Failed to retrieve users in group from identity provider.");
}