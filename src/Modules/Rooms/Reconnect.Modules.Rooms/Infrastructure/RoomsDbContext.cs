using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Rooms.Infrastructure;

internal sealed class RoomsDbContext(DbContextOptions<RoomsDbContext> options) : DbContext(options)
{
    public const string Schema = "rooms";

    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Room>(room =>
        {
            room.Property(r => r.Name).HasMaxLength(Room.NameMaxLength);
            room.Property(r => r.Theme).HasMaxLength(RoomThemes.MaxLength).HasDefaultValue(RoomThemes.Cozy);
            room.Property(r => r.Width).HasDefaultValue(Room.DefaultSize);
            room.Property(r => r.Depth).HasDefaultValue(Room.DefaultSize);
            room.Property(r => r.Capacity).HasDefaultValue(Room.DefaultCapacity);

            // Layout is stored as a single jsonb column: [{ ItemId, Position: { X, Y, Z }, Rotation, Colours }, ...]
            room.OwnsMany(r => r.Layout, item =>
            {
                item.ToJson();
                item.OwnsOne(i => i.Position);
                item.Property(i => i.Colours).HasMaxLength(RoomItem.ColoursMaxLength);
            });

            // Outline of tower floors as jsonb: [{ X, Z }, ...]
            room.OwnsMany(r => r.Outline, point => point.ToJson());
            room.Ignore(r => r.HasOutline);

            // OwnerId / BuildingId point into other modules: no foreign keys across module boundaries.
            room.HasIndex(r => r.OwnerId);
            room.HasIndex(r => new { r.BuildingId, r.IsPublic });
            room.HasIndex(r => new { r.BuildingId, r.Floor });
        });
    }
}

internal sealed class RoomsDesignTimeFactory : ModuleDesignTimeFactory<RoomsDbContext>
{
    protected override string Schema => RoomsDbContext.Schema;
}
