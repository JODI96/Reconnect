using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;
using Reconnect.Modules.City.Domain;
using Reconnect.Modules.City.Infrastructure;
using Reconnect.Modules.City.Public;

namespace Reconnect.Modules.City.Features;

internal static class BuildingEndpoints
{
    public const double DefaultRadiusMeters = 500;
    public const double MaxRadiusMeters = 5000;
    public const int MaxResults = 100;

    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Buildings.Path).WithTags("Buildings").RequireAuthorization();
        group.MapGet("/nearby", Nearby);
    }

    /// <summary>Buildings within <paramref name="radiusMeters"/> of a point, nearest first (PostGIS ST_DWithin).</summary>
    private static async Task<Results<Ok<List<BuildingDto>>, ValidationProblem>> Nearby(
        double lat, double lng, double? radiusMeters, CityDbContext db, CancellationToken ct)
    {
        var radius = radiusMeters ?? DefaultRadiusMeters;
        var errors = new Dictionary<string, string[]>();
        if (lat is < -90 or > 90)
        {
            errors["lat"] = ["Latitude must be between -90 and 90."];
        }
        if (lng is < -180 or > 180)
        {
            errors["lng"] = ["Longitude must be between -180 and 180."];
        }
        if (radius is <= 0 or > MaxRadiusMeters)
        {
            errors["radiusMeters"] = [$"Radius must be between 0 and {MaxRadiusMeters} metres."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var origin = Building.CreatePoint(lat, lng);
        var rows = await db.Buildings
            .Where(b => b.Location.IsWithinDistance(origin, radius))
            .OrderBy(b => b.Location.Distance(origin))
            .Take(MaxResults)
            .Select(b => new { b.Id, b.Name, b.Address, b.Location, Distance = b.Location.Distance(origin) })
            .ToListAsync(ct);

        // Coordinates are read from the loaded point (ST_X/ST_Y don't accept geography).
        return TypedResults.Ok(rows
            .Select(b => new BuildingDto(b.Id, b.Name, b.Address, b.Location.Y, b.Location.X, b.Distance))
            .ToList());
    }
}

internal sealed class CityDirectory(CityDbContext db) : ICityDirectory
{
    public Task<bool> BuildingExistsAsync(Guid buildingId, CancellationToken ct) =>
        db.Buildings.AnyAsync(b => b.Id == buildingId, ct);
}
