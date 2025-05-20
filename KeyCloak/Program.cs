using System.Security.Claims;
using System.Text.Json.Serialization;
using KeyCloak.Application;
using KeyCloak.Application.Services.GroupsService;
using KeyCloak.Application.Services.RolesExtractionService;
using KeyCloak.Application.Services.UsersAccount;
using KeyCloak.Application.Services.UsersEmailService;
using KeyCloak.Infrastructure.Identity;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakAuthClients;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakUserClients;
using KeyCloak.Infrastructure.Identity.Services.GroupsService;
using KeyCloak.Infrastructure.Identity.Services.RolesExtractionService;
using KeyCloak.Infrastructure.Identity.Services.UsersAccount;
using KeyCloak.Infrastructure.Identity.Services.UsersEmailService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using KeyCloak.Application.Services.DealersService;
using KeyCloak.Infrastructure.Identity.Services.DealersService;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakDealersClients;
using KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakGroupClients;
using Microsoft.AspNetCore.Authorization;
using KeyCloak.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ===== Configuration =====
builder.Services.Configure<KeyCloakOptions>(builder.Configuration.GetSection("KeyCloak"));
var keycloakSettings = builder.Configuration.GetSection("KeyCloak");

// ===== HttpContext Accessor =====
builder.Services.AddHttpContextAccessor();

// ===== HTTP Clients =====
builder.Services.AddHttpClient<KeycloakAuthClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddHttpClient<KeycloakUserClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddHttpClient<KeycloakGroupClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddHttpClient<KeyCloakClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddHttpClient<KeycloakDealerClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});

// ===== MediatR =====
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyReference).Assembly);
});

// ===== Application Services =====
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IUserEmailService, UserEmailService>();
builder.Services.AddScoped<IGroupManagementService, GroupManagementService>();
builder.Services.AddScoped<IUserGroupQueryService, UserGroupQueryService>();
builder.Services.AddScoped<IRoleExtractionService, RoleExtractionService>();

// ===== Dealer Management =====
builder.Services.AddScoped<IDealerManagementService, DealerManagementService>();

// ===== Authentication: JWT Bearer =====
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = keycloakSettings["Authority"];
    options.RequireHttpsMetadata = false;
    options.Audience = "dotnet-api-client"; // Match your Keycloak client_id

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = keycloakSettings["Issuer"],
        ValidateAudience = false,
        ValidateLifetime = true,
        RoleClaimType = ClaimTypes.Role
    };
});

// ===== Authorization Policies =====
builder.Services.AddAuthorization(options =>
{
    // Basic role-based policy
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

// We still need this for other users
builder.Services.AddSingleton<IAuthorizationHandler, KeycloakPermissionHandler>();

// Register the authorization handler for Keycloak permissions
builder.Services.AddSingleton<IAuthorizationHandler, KeycloakPermissionHandler>();

// ===== API Versioning =====
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// ===== Controllers =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KeyCloak API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== App Pipeline =====
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<KeycloakAuthorizationMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();