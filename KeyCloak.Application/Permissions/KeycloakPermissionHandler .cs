using KeyCloak.Application.Permissions;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

public class KeycloakPermissionHandler : AuthorizationHandler<KeycloakPermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                KeycloakPermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? false)
        {
            return Task.CompletedTask;
        }

        // Check if user is a super-admin
        if (context.User.IsInRole("super-admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check for resource_access claim
        var resourceAccessClaim = context.User.FindFirst("resource_access");
        if (resourceAccessClaim != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(resourceAccessClaim.Value);

                // Looking for specific client permissions
                if (doc.RootElement.TryGetProperty("dotnet-api-client", out var clientAccess))
                {
                    if (clientAccess.TryGetProperty("roles", out var roles))
                    {
                        // Check for permission-specific role
                        foreach (var role in roles.EnumerateArray())
                        {
                            var roleStr = role.GetString();
                            if (roleStr == "view-dealer-management-permission" ||
                                roleStr == $"{requirement.Resource}:{requirement.Scope}")
                            {
                                context.Succeed(requirement);
                                return Task.CompletedTask;
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid JSON format, continue with other checks
            }
        }

        // Check for authorization data that might come from Keycloak Authorization Services
        var authorizationClaim = context.User.FindFirst("authorization");
        if (authorizationClaim != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(authorizationClaim.Value);
                if (doc.RootElement.TryGetProperty("permissions", out var permissions))
                {
                    foreach (var permission in permissions.EnumerateArray())
                    {
                        if (permission.TryGetProperty("rsname", out var rsname) &&
                            permission.TryGetProperty("scopes", out var scopes))
                        {
                            var resourceName = rsname.GetString();

                            if (resourceName == requirement.Resource)
                            {
                                foreach (var scope in scopes.EnumerateArray())
                                {
                                    if (scope.GetString() == requirement.Scope)
                                    {
                                        context.Succeed(requirement);
                                        return Task.CompletedTask;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid JSON format, authorization check fails
            }
        }

        return Task.CompletedTask;
    }
}