using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Reconnect.Infrastructure.Identity;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Common.Auth;

public static class AuthSetup
{
    public static IHostApplicationBuilder AddReconnectAuth(this IHostApplicationBuilder builder)
    {
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
            .AddEntityFrameworkStores<ReconnectDbContext>()
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
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
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
                NameClaimType = JwtRegisteredClaimNames.Sub,
                ClockSkew = TimeSpan.FromSeconds(30),
            });

        builder.Services.AddAuthorization();
        builder.Services.AddScoped<TokenService>();
        builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

        return builder;
    }

    /// <summary>SignalR addresses users by the JWT "sub" claim (= user id).</summary>
    private sealed class SubClaimUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) =>
            connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    }
}
