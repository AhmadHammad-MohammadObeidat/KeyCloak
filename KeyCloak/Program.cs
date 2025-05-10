using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Users.RegisterUser;
using KeyCloak.Domian;
using KeyCloak.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ✅ MediatR setup
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
});

// ✅ Configure Keycloak settings
builder.Services.Configure<KeyCloakOptions>(
    builder.Configuration.GetSection("KeyCloak"));

// ✅ Register Keycloak client and identity service
builder.Services.AddHttpClient<KeyCloakClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddScoped<IIdentityProviderService, IdentityProviderService>();

// ✅ JWT Authentication using Keycloak
var keycloakSettings = builder.Configuration.GetSection("KeyCloak");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = keycloakSettings["Authority"];
    options.Audience = keycloakSettings["Audience"];
    options.RequireHttpsMetadata = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        RoleClaimType = ClaimTypes.Role, // TEMPORARY, used below
        ValidateIssuer = true,
        ValidIssuer = keycloakSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = keycloakSettings["Audience"],
        ValidateLifetime = true
    };

    // 🧠 This adds "realm_access.roles" to ClaimsPrincipal
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var user = context.Principal;

            if (user?.Identity is ClaimsIdentity identity)
            {
                var realmAccess = user.FindFirst("realm_access")?.Value;
                if (realmAccess != null)
                {
                    var parsed = System.Text.Json.JsonDocument.Parse(realmAccess);
                    if (parsed.RootElement.TryGetProperty("roles", out var rolesElement))
                    {
                        foreach (var role in rolesElement.EnumerateArray())
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()));
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    };
});

// ✅ Authorization policies that match Keycloak roles
builder.Services.AddAuthorization(options =>
{
    // Generic admin policy
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("admin", "group-admin-demo", "group-admin-real", "group-admin-pending"));

    // DEMO policies using Keycloak's actual role
    options.AddPolicy("CreateDemoUser", policy =>
        policy.RequireRole("admin-demo-create"));
    options.AddPolicy("GetDemoUser", policy =>
        policy.RequireRole("admin-demo-get"));
    options.AddPolicy("UpdateDemoUser", policy =>
        policy.RequireRole("admin-demo-update"));

    // REAL policies
    options.AddPolicy("CreateRealUser", policy =>
        policy.RequireRole("admin-real-create"));
    options.AddPolicy("GetRealUser", policy =>
        policy.RequireRole("admin-real-get"));
    options.AddPolicy("UpdateRealUser", policy =>
        policy.RequireRole("admin-real-update"));

    // PENDING policies
    options.AddPolicy("CreatePendingUser", policy =>
        policy.RequireRole("admin-pending-create"));
    options.AddPolicy("GetPendingUser", policy =>
        policy.RequireRole("admin-pending-get"));
    options.AddPolicy("UpdatePendingUser", policy =>
        policy.RequireRole("admin-pending-update"));
});

// ✅ API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ✅ Controllers and JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ✅ Swagger support (optional)
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ✅ Middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
