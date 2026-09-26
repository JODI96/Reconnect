using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;
using Reconnect.Modules.Identity.Infrastructure;
using Reconnect.Modules.Identity.Public;
using Reconnect.SharedKernel.Domain;
using Reconnect.SharedKernel.Events;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Identity.Features;

internal static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Auth.Path).WithTags("Auth").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> Register(
        RegisterRequest request, UserManager<AppUser> users, TokenService tokens, IEventBus events,
        TimeProvider time, CancellationToken ct)
    {
        var birthDate = DateOnly.FromDateTime(request.BirthDate);
        if (!AgePolicy.IsAdult(birthDate, time.GetUtcToday()))
        {
            return Validation.Problem(nameof(request.BirthDate), $"You must be at least {AgePolicy.MinimumAge} years old.");
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = time.GetUtcNow(),
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(result.Errors
                .GroupBy(e => e.Code.Contains("Password", StringComparison.Ordinal) ? "Password" : "Email")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        try
        {
            // The Profiles module creates the profile (and validates the display name).
            await events.PublishAsync(new UserRegistered(user.Id, request.DisplayName, birthDate), ct);
        }
        catch (DomainException ex)
        {
            await users.DeleteAsync(user);   // compensate: no account without a profile
            return Validation.Problem(nameof(request.DisplayName), ex.Message);
        }

        return TypedResults.Ok(await tokens.IssueAsync(user, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Login(
        LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, TokenService tokens,
        CancellationToken ct)
    {
        // "Email" accepts the email address or the user name (registered users: user name = email).
        var user = await users.FindByEmailAsync(request.Email) ?? await users.FindByNameAsync(request.Email);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        return result.Succeeded
            ? TypedResults.Ok(await tokens.IssueAsync(user, ct))
            : TypedResults.Unauthorized();
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Refresh(
        RefreshRequest request, TokenService tokens, CancellationToken ct)
    {
        var response = await tokens.RefreshAsync(request.RefreshToken, ct);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }
}
