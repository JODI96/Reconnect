using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.RealEstate.Domain;
using Reconnect.Modules.RealEstate.Features;
using Reconnect.Modules.Rooms.Public;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.RealEstate.Infrastructure;

internal sealed class RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : DbContext(options)
{
    public const string Schema = "realestate";

    public DbSet<OfficeUnit> Offices => Set<OfficeUnit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<OfficeUnit>(unit =>
        {
            unit.ToTable("offices");
            unit.Ignore(u => u.IsAvailable);
            unit.Property(u => u.Name).HasMaxLength(OfficeUnit.NameMaxLength);
            // BuildingId/OwnerId/RoomId point into other modules: no foreign keys across module boundaries.
            unit.HasIndex(u => new { u.BuildingId, u.Floor });
            unit.HasIndex(u => u.OwnerId);
        });
    }
}

internal sealed class RealEstateDesignTimeFactory : ModuleDesignTimeFactory<RealEstateDbContext>
{
    protected override string Schema => RealEstateDbContext.Schema;
}

/// <summary>The offer of offices (all environments – it's part of the game). Idempotent.</summary>
internal static class OfficeSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<RealEstateDbContext>();
        var existing = await db.Offices.Where(o => o.BuildingId == ZurichBuildings.PrimeTowerId).ToDictionaryAsync(o => o.Id, ct);
        var offer = PrimeTowerOffices.Offer().ToList();
        // Units that are no longer offered (half storeys of earlier versions) go away unless someone owns them.
        foreach (var gone in existing.Values.Where(u => u.IsAvailable && offer.All(o => o.Id != u.Id)))
        {
            db.Offices.Remove(gone);
        }
        foreach (var (id, floor, name, price, area) in offer)
        {
            if (existing.TryGetValue(id, out var unit))
            {
                unit.UpdateOffer(name, price, area);
            }
            else
            {
                db.Offices.Add(OfficeUnit.Create(id, ZurichBuildings.PrimeTowerId, floor, name, price, area));
            }
        }
        await db.SaveChangesAsync(ct);

        // A bought office always has its room: one that went missing (e.g. removed by an older seeding) is created again.
        var rooms = services.GetRequiredService<IRoomProvisioning>();
        foreach (var unit in await db.Offices.AsNoTracking().Where(o => o.OwnerId != null).ToListAsync(ct))
        {
            if (unit.RoomId is { } roomId && await rooms.RoomExistsAsync(roomId, ct))
            {
                continue;
            }
            var created = await rooms.CreateOfficeAsync(unit.OwnerId!.Value, unit.BuildingId, unit.Floor, unit.Name, OfficeEndpoints.OfficeCapacity, ct);
            await db.Offices.Where(o => o.Id == unit.Id).ExecuteUpdateAsync(o => o.SetProperty(x => x.RoomId, created), ct);
        }
    }
}
