using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;
using Reconnect.Domain.Buildings;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Buildings;

public sealed class BuildingEndpoints : IEndpointModule
{
    public const double DefaultRadiusMeters = 500;
    public const double MaxRadiusMeters = 5000;
    public const int MaxResults = 100;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Buildings.Group).WithTags("Buildings").RequireAuthorization();

        group.MapGet("/nearby", Nearby);
    }

    /// <summary>Buildings within <paramref name="radiusMeters"/> of a point, nearest first (PostGIS ST_DWithin).</summary>
    private static async Task<Results<Ok<List<BuildingDto>>, ValidationProblem>> Nearby(
        double lat, double lng, double? radiusMeters, ReconnectDbContext db, CancellationToken ct)
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
        var buildings = await db.Buildings
            .Where(b => b.Location.IsWithinDistance(origin, radius))
            .OrderBy(b => b.Location.Distance(origin))
            .Take(MaxResults)
            .Select(b => new BuildingDto(b.Id, b.Name, b.Address, b.Location.Y, b.Location.X, b.Location.Distance(origin)))
            .ToListAsync(ct);

        return TypedResults.Ok(buildings);
    }
}
