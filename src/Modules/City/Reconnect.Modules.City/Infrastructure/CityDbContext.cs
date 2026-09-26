using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.City.Domain;
using Reconnect.Modules.City.Public;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.City.Infrastructure;

internal sealed class CityDbContext(DbContextOptions<CityDbContext> options) : DbContext(options)
{
    public const string Schema = "city";

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<MapUsage> MapUsage => Set<MapUsage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.HasPostgresExtension("postgis");

        builder.Entity<Building>(building =>
        {
            building.Property(b => b.Name).HasMaxLength(200);
            building.Property(b => b.Address).HasMaxLength(300);

            // geography => distance calculations in metres (ST_DWithin / ST_Distance).
            building.Property(b => b.Location).HasColumnType($"geography (point, {Building.Srid})");
            building.Property(b => b.Footprint).HasColumnType($"geography (polygon, {Building.Srid})");
            building.HasIndex(b => b.Location).HasMethod("gist");

            building.HasData(SampleBuildings);
        });

        builder.Entity<MapUsage>(usage =>
        {
            usage.ToTable("map_usage");
            usage.HasKey(u => new { u.UserId, u.Month });
            usage.HasIndex(u => new { u.Month, u.IsPremium });
        });
    }

    private static NetTopologySuite.Geometries.Polygon PrimeTowerFootprint()
    {
        var ring = Footprints.PrimeTower.Append(Footprints.PrimeTower[0])
            .Select(p => new NetTopologySuite.Geometries.Coordinate(p.Lon, p.Lat))
            .ToArray();
        return new NetTopologySuite.Geometries.Polygon(new NetTopologySuite.Geometries.LinearRing(ring)) { SRID = Building.Srid };
    }

    /// <summary>Development sample buildings (part of the migration via HasData).</summary>
    private static IReadOnlyList<Building> SampleBuildings =>
    [
        Building.Create(ZurichBuildings.HauptbahnhofId, "Zürich HB", "Bahnhofplatz 1, 8001 Zürich", 47.37785, 8.54018),
        Building.Create(ZurichBuildings.GrossmuensterId, "Grossmünster", "Grossmünsterplatz, 8001 Zürich", 47.37011, 8.54411),
        Building.Create(ZurichBuildings.PrimeTowerId, "Prime Tower", "Hardstrasse 201, 8005 Zürich", 47.38622, 8.51733, PrimeTowerFootprint()),
        Building.Create(ZurichBuildings.EthHauptgebaeudeId, "ETH Hauptgebäude", "Rämistrasse 101, 8092 Zürich", 47.37635, 8.54798),
        Building.Create(ZurichBuildings.OpernhausId, "Opernhaus Zürich", "Falkenstrasse 1, 8008 Zürich", 47.36490, 8.54671),
        Building.Create(ZurichBuildings.LandesmuseumId, "Landesmuseum", "Museumstrasse 2, 8001 Zürich", 47.37926, 8.54063),
        Building.Create(ZurichBuildings.KunsthausId, "Kunsthaus Zürich", "Heimplatz 1, 8001 Zürich", 47.37036, 8.54826),
    ];
}

/// <summary>Footprints of towers (OpenStreetMap way 47122541, © OpenStreetMap contributors, ODbL).</summary>
internal static class Footprints
{
    public static readonly (double Lat, double Lon)[] PrimeTower =
    [
        (47.3858168, 8.5172916), (47.3858427, 8.5173161), (47.3859997, 8.5174734), (47.3860325, 8.5174691),
        (47.3861503, 8.5174531), (47.3862611, 8.5174915), (47.3862929, 8.5175038), (47.3864032, 8.5172469),
        (47.3863787, 8.5172227), (47.3862219, 8.5170682), (47.3861908, 8.5170710), (47.3860271, 8.5170859),
        (47.3859049, 8.5170304), (47.3858759, 8.5170173),
    ];
}

internal sealed class CityDesignTimeFactory : ModuleDesignTimeFactory<CityDbContext>
{
    protected override string Schema => CityDbContext.Schema;
    protected override bool Spatial => true;
}
