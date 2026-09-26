using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.City.Domain;
using Reconnect.Modules.City.Public;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.City.Infrastructure;

internal sealed class CityDbContext(DbContextOptions<CityDbContext> options) : DbContext(options)
{
    public const string Schema = "city";

    public DbSet<Building> Buildings => Set<Building>();

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
    }

    /// <summary>Development sample buildings (part of the migration via HasData).</summary>
    private static IReadOnlyList<Building> SampleBuildings =>
    [
        Building.Create(ZurichBuildings.HauptbahnhofId, "Zürich HB", "Bahnhofplatz 1, 8001 Zürich", 47.37785, 8.54018),
        Building.Create(ZurichBuildings.GrossmuensterId, "Grossmünster", "Grossmünsterplatz, 8001 Zürich", 47.37011, 8.54411),
        Building.Create(ZurichBuildings.PrimeTowerId, "Prime Tower", "Hardstrasse 201, 8005 Zürich", 47.38622, 8.51733),
        Building.Create(ZurichBuildings.EthHauptgebaeudeId, "ETH Hauptgebäude", "Rämistrasse 101, 8092 Zürich", 47.37635, 8.54798),
        Building.Create(ZurichBuildings.OpernhausId, "Opernhaus Zürich", "Falkenstrasse 1, 8008 Zürich", 47.36490, 8.54671),
        Building.Create(ZurichBuildings.LandesmuseumId, "Landesmuseum", "Museumstrasse 2, 8001 Zürich", 47.37926, 8.54063),
        Building.Create(ZurichBuildings.KunsthausId, "Kunsthaus Zürich", "Heimplatz 1, 8001 Zürich", 47.37036, 8.54826),
    ];
}

internal sealed class CityDesignTimeFactory : ModuleDesignTimeFactory<CityDbContext>
{
    protected override string Schema => CityDbContext.Schema;
    protected override bool Spatial => true;
}
