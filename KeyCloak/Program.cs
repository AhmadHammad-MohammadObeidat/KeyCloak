using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Users.RegisterUser;
using KeyCloak.Domian;
using KeyCloak.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ✅ MediatR for application layer
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
});

// ✅ Keycloak options binding
builder.Services.Configure<KeyCloakOptions>(
    builder.Configuration.GetSection("KeyCloak"));

// ✅ HTTP client for Keycloak admin API
builder.Services.AddHttpClient<KeyCloakClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddScoped<IIdentityProviderService, IdentityProviderService>();

// ✅ Authentication via JWT Bearer tokens from Keycloak
var keycloakSettings = builder.Configuration.GetSection("KeyCloak");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = keycloakSettings["Authority"]; // e.g., http://localhost:8080/realms/KeyCloakDotNetReleam
    options.RequireHttpsMetadata = false;
    options.Audience = "account"; // or keycloakSettings["Audience"]

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = keycloakSettings["Issuer"],
        ValidateAudience = false, // ✅ Skip audience validation (or use "account")
        ValidateLifetime = true,
        RoleClaimType = ClaimTypes.Role
    };

    // ✅ Extract realm_access.roles as individual role claims
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var user = context.Principal;
            if (user?.Identity is ClaimsIdentity identity)
            {
                var realmAccess = user.FindFirst("realm_access")?.Value;
                if (!string.IsNullOrWhiteSpace(realmAccess))
                {
                    using var doc = JsonDocument.Parse(realmAccess);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
                    {
                        foreach (var role in rolesElement.EnumerateArray())
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString() ?? ""));
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    };
});

// ✅ Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("admin", "group-admin-demo", "group-admin-real", "group-admin-pending"));

    options.AddPolicy("GetDemoUser", policy =>
        policy.RequireRole("Get-Demo-Group")); // Example Keycloak role
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

// ✅ Add controllers and JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ✅ Swagger + JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KeyCloak API", Version = "v1" });

    // JWT auth support in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token."
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

var app = builder.Build();

// ✅ Middleware setup
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // 🔥 Required to populate ClaimsPrincipal
app.UseAuthorization();

app.MapControllers();
app.Run();
