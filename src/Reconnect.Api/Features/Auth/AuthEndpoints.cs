using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Reconnect.Api.Common;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;
using Reconnect.Domain.Profiles;
using Reconnect.Infrastructure.Identity;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Auth;

public sealed class AuthEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Auth.Group).WithTags("Auth").AllowAnonymous();

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> Register(
        RegisterRequest request, UserManager<AppUser> users, ReconnectDbContext db, TokenService tokens,
        TimeProvider time, CancellationToken ct)
    {
        var birthDate = DateOnly.FromDateTime(request.BirthDate);
        if (!AgePolicy.IsAdult(birthDate, time.GetUtcToday()))
        {
            return Problem(nameof(request.BirthDate), $"You must be at least {AgePolicy.MinimumAge} years old.");
        }
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > Profile.DisplayNameMaxLength)
        {
            return Problem(nameof(request.DisplayName), $"Display name must be 1-{Profile.DisplayNameMaxLength} characters.");
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = time.GetUtcNow(),
        };

        // Tracked before CreateAsync so user and profile are saved in the same SaveChanges (atomic).
        db.Profiles.Add(Profile.Create(user.Id, request.DisplayName, birthDate, time.GetUtcToday()));

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(result.Errors
                .GroupBy(e => e.Code.Contains("Password", StringComparison.Ordinal) ? "Password" : "Email")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        return TypedResults.Ok(await tokens.IssueAsync(user, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Login(
        LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, TokenService tokens,
        CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
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

    private static ValidationProblem Problem(string field, string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [error] });
}
