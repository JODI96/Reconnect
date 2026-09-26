using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.RealEstate.Domain;
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
        foreach (var (id, floor, name, price, area) in PrimeTowerOffices.Offer())
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
    }
}
