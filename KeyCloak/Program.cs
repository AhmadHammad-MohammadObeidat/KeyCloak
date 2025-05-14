using KeyCloak.Application.Abstractions.Identity;
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

// ✅ Register MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(IIdentityProviderService).Assembly);
});

builder.Services.AddHttpContextAccessor();

// ✅ Register Keycloak client + Identity service
builder.Services.Configure<KeyCloakOptions>(
    builder.Configuration.GetSection("KeyCloak"));

builder.Services.AddHttpClient<KeyCloakClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080/admin/realms/KeyCloakDotNetReleam/");
});
builder.Services.AddScoped<IIdentityProviderService, IdentityProviderService>();

// ✅ Configure JWT Bearer Authentication
var keycloakSettings = builder.Configuration.GetSection("KeyCloak");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = keycloakSettings["Authority"];
    options.RequireHttpsMetadata = false;
    options.Audience = "dotnet-api-client"; // or "dotnet-api-client"

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = keycloakSettings["Issuer"],
        ValidateAudience = false,
        ValidateLifetime = true,
        RoleClaimType = ClaimTypes.Role
    };

    // ✅ Extract roles from realm_access.roles[]
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var identity = context.Principal?.Identity as ClaimsIdentity;
            var realmAccess = context.Principal?.FindFirst("realm_access")?.Value;

            if (!string.IsNullOrWhiteSpace(realmAccess))
            {
                using var doc = JsonDocument.Parse(realmAccess);
                if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
                {
                    foreach (var role in rolesElement.EnumerateArray())
                    {
                        identity?.AddClaim(new Claim(ClaimTypes.Role, role.GetString() ?? ""));
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
    options.AddPolicy("GroupViewerPolicy", policy =>
        policy.RequireAuthenticatedUser().RequireRole("group-viewer"));
});

// ✅ API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// ✅ Register controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ✅ Swagger with JWT auth support
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

var app = builder.Build();

// ✅ Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
