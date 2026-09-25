using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Api.Features.Blocks;
using Reconnect.Contracts;
using Reconnect.Contracts.Profiles;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Profiles;

public sealed class ProfileEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Profiles.Group).WithTags("Profiles").RequireAuthorization();

        group.MapGet("/me", GetMine);
        group.MapPut("/me", UpdateMine);
        group.MapGet("/{id:guid}", GetById);
    }

    private static async Task<Results<Ok<MyProfileDto>, NotFound>> GetMine(
        ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        var dto = await QueryMine(db, principal.GetUserId(), ct);
        return dto is null ? TypedResults.NotFound() : TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<MyProfileDto>, NotFound>> UpdateMine(
        UpdateProfileRequest request, ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        profile.Update(request.DisplayName, request.Bio);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok((await QueryMine(db, userId, ct))!);
    }

    /// <summary>Blocked users (either direction) get 404 – they must not see each other.</summary>
    private static async Task<Results<Ok<ProfileDto>, NotFound>> GetById(
        Guid id, ClaimsPrincipal principal, ReconnectDbContext db, TimeProvider time, CancellationToken ct)
    {
        var hiddenUserIds = db.HiddenUserIdsFor(principal.GetUserId());
        var profile = await db.Profiles
            .Where(p => p.UserId == id && !hiddenUserIds.Contains(p.UserId))
            .SingleOrDefaultAsync(ct);

        return profile is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(profile.ToDto(time.GetUtcToday()));
    }

    private static Task<MyProfileDto?> QueryMine(ReconnectDbContext db, Guid userId, CancellationToken ct) =>
        db.Profiles
            .Where(p => p.UserId == userId)
            .Join(db.Users, p => p.UserId, u => u.Id, (p, u) => new { Profile = p, u.Email })
            .Select(x => new MyProfileDto(
                x.Profile.UserId, x.Email!, x.Profile.DisplayName,
                x.Profile.BirthDate.ToDateTime(TimeOnly.MinValue), x.Profile.Bio, x.Profile.IsVerified))
            .SingleOrDefaultAsync(ct);
}
