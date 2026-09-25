using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconnect.Domain.Buildings;
using Reconnect.Infrastructure.Persistence.Seed;

namespace Reconnect.Infrastructure.Persistence.Configurations;

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200);
        builder.Property(b => b.Address).HasMaxLength(300);

        // geography => distance calculations in metres (ST_DWithin / ST_Distance).
        builder.Property(b => b.Location).HasColumnType($"geography (point, {Building.Srid})");
        builder.Property(b => b.Footprint).HasColumnType($"geography (polygon, {Building.Srid})");
        builder.HasIndex(b => b.Location).HasMethod("gist");

        builder.HasData(ZurichBuildings.All);
    }
}
