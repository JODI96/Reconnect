using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Reconnect.Modules.Identity.Features;
using Reconnect.Modules.Identity.Infrastructure;
using Reconnect.Modules.Identity.Public;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Identity;

/// <summary>
/// Accounts and authentication: registration, login, JWT access tokens, rotating refresh tokens,
/// the development admin account. Other modules only see <see cref="IUserDirectory"/> and the
/// <see cref="UserRegistered"/> event.
/// </summary>
public sealed class IdentityModule : IModule
{
    public string Name => "identity";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<IdentityDbContext>(Name);

        builder.Services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Keep claim names as issued ("sub" instead of the long XML claim type).
                o.MapInboundClaims = false;
                o.Events = new JwtBearerEvents
                {
                    // WebSockets can't send headers: SignalR passes the token as query parameter.
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.Value?.Contains("/hubs/", StringComparison.Ordinal) == true)
                        {
                            ctx.Token = token;
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        // Token validation depends on JwtOptions, which are only known once configuration is bound.
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwt) => o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Value.Issuer,
                ValidAudience = jwt.Value.Audience,
                IssuerSigningKey = TokenService.CreateSigningKey(jwt.Value.SigningKey),
                NameClaimType = ClaimsPrincipalExtensions.SubjectClaim,
                RoleClaimType = ClaimsPrincipalExtensions.RoleClaim,
                ClockSkew = TimeSpan.FromSeconds(30),
            });

        builder.Services.AddAuthorization();
        builder.Services.AddScoped<TokenService>();
        builder.Services.AddScoped<IUserDirectory, UserDirectory>();
        builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => AuthEndpoints.Map(api);

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<IdentityDbContext>(ct);

    public Task SeedAsync(IServiceProvider services, CancellationToken ct) => DevAdminSeeder.SeedIfEnabledAsync(services, ct);

    /// <summary>SignalR addresses users by the JWT "sub" claim (= user id).</summary>
    private sealed class SubClaimUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) =>
            connection.User.FindFirst(ClaimsPrincipalExtensions.SubjectClaim)?.Value;
    }
}
