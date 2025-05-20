using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace KeyCloak.Api.Extensions;

public static class KeycloakAuthorizationExtensions
{
    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloakSettings = configuration.GetSection("KeyCloak");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = keycloakSettings["Authority"];
            options.RequireHttpsMetadata = false;
            options.Audience = "dotnet-api-client";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = keycloakSettings["Issuer"],
                ValidateAudience = false,
                ValidateLifetime = true,
                RoleClaimType = ClaimTypes.Role
            };
        });

        return services;
    }

    public static IServiceCollection AddKeycloakAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("GroupViewerPolicy", policy =>
                policy.RequireAuthenticatedUser().RequireRole("group-viewer"));

            options.AddPolicy("view-dealer-management", policy =>
                policy.RequireAssertion(context =>
                {
                    if (context.Resource is HttpContext httpContext &&
                        httpContext.Items.TryGetValue("KeycloakPermissions", out var permsObj) &&
                        permsObj is List<(string Resource, List<string> Scopes)> permissions)
                    {
                        return permissions.Any(p =>
                            p.Resource == "dealer-management" &&
                            p.Scopes.Contains("view"));
                    }

                    return false;
                }));
        });

        return services;
    }
}