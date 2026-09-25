using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconnect.Domain.Buildings;
using Reconnect.Domain.Rooms;
using Reconnect.Infrastructure.Identity;

namespace Reconnect.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.Name).HasMaxLength(Room.NameMaxLength);
        builder.Property(r => r.Theme).HasMaxLength(RoomThemes.MaxLength).HasDefaultValue(RoomThemes.Cozy);

        // Layout is stored as a single jsonb column: [{ ItemId, Position: { X, Y, Z }, Rotation }, ...]
        builder.OwnsMany(r => r.Layout, item =>
        {
            item.ToJson();
            item.OwnsOne(i => i.Position);
        });

        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Building>().WithMany().HasForeignKey(r => r.BuildingId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.OwnerId);
        builder.HasIndex(r => new { r.BuildingId, r.IsPublic });
    }
}
