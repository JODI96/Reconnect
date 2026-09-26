using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.Profiles;
using Reconnect.Modules.Identity.Public;
using Reconnect.Modules.Profiles.Domain;
using Reconnect.Modules.Profiles.Infrastructure;
using Reconnect.Modules.Profiles.Public;
using Reconnect.Modules.Safety.Public;
using Reconnect.SharedKernel.Events;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Profiles.Features;

internal static class ProfileEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Profiles.Path).WithTags("Profiles").RequireAuthorization();
        group.MapGet("/me", GetMine);
        group.MapPut("/me", UpdateMine);
        group.MapGet("/{id:guid}", GetById);
    }

    private static async Task<Results<Ok<MyProfileDto>, NotFound>> GetMine(ClaimsPrincipal principal, ProfilesDbContext db, CancellationToken ct)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == principal.GetUserId(), ct);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ToMyDto(profile, principal));
    }

    private static async Task<Results<Ok<MyProfileDto>, NotFound>> UpdateMine(
        UpdateProfileRequest request, ClaimsPrincipal principal, ProfilesDbContext db, CancellationToken ct)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == principal.GetUserId(), ct);
        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        profile.Update(request.DisplayName, request.Bio);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToMyDto(profile, principal));
    }

    /// <summary>Blocked users (either direction) get 404 – they must not see each other.</summary>
    private static async Task<Results<Ok<ProfileDto>, NotFound>> GetById(
        Guid id, ClaimsPrincipal principal, ProfilesDbContext db, IBlockQueries blocks, TimeProvider time, CancellationToken ct)
    {
        if (await blocks.IsBlockedBetweenAsync(principal.GetUserId(), id, ct))
        {
            return TypedResults.NotFound();
        }

        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == id, ct);
        return profile is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new ProfileDto(profile.UserId, profile.DisplayName, profile.AgeOn(time.GetUtcToday()), profile.Bio, profile.IsVerified));
    }

    private static MyProfileDto ToMyDto(Profile profile, ClaimsPrincipal principal) =>
        new(profile.UserId, principal.GetEmail(), profile.DisplayName, profile.BirthDate.ToDateTime(TimeOnly.MinValue),
            profile.Bio, profile.IsVerified);
}

internal sealed class ProfileDirectory(ProfilesDbContext db) : IProfileDirectory
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => db.Profiles.AnyAsync(p => p.UserId == userId, ct);

    public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct) =>
        db.Profiles.Where(p => p.UserId == userId).Select(p => (string?)p.DisplayName).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        return await db.Profiles.Where(p => ids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
    }
}

/// <summary>Creates the profile for a new account (idempotent – the dev admin seeding publishes on every start).</summary>
internal sealed class CreateProfileOnRegistration(ProfilesDbContext db, TimeProvider time) : IIntegrationEventHandler<UserRegistered>
{
    public async Task HandleAsync(UserRegistered integrationEvent, CancellationToken ct)
    {
        if (await db.Profiles.AnyAsync(p => p.UserId == integrationEvent.UserId, ct))
        {
            return;
        }
        db.Profiles.Add(Profile.Create(integrationEvent.UserId, integrationEvent.DisplayName, integrationEvent.BirthDate, time.GetUtcToday()));
        await db.SaveChangesAsync(ct);
    }
}
