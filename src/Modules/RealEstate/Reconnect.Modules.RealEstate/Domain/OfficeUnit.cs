using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.RealEstate.Domain;

/// <summary>
/// A private office on a storey of a building that users can buy and sell. Buying creates the owner's room
/// on that floor (Rooms module), selling removes it. Ownership changes are single atomic SQL updates.
/// </summary>
internal sealed class OfficeUnit
{
    public const int NameMaxLength = 80;

    private OfficeUnit() { }

    public Guid Id { get; private set; }
    public Guid BuildingId { get; private set; }
    public int Floor { get; private set; }
    public string Name { get; private set; } = "";
    public long PriceRappen { get; private set; }
    public int AreaSquareMeters { get; private set; }
    public Guid? OwnerId { get; private set; }
    public Guid? RoomId { get; private set; }
    public DateTimeOffset? PurchasedAt { get; private set; }

    public bool IsAvailable => OwnerId is null;

    public static OfficeUnit Create(Guid id, Guid buildingId, int floor, string name, long priceRappen, int areaSquareMeters)
    {
        if (priceRappen <= 0)
        {
            throw new DomainException("An office needs a price.");
        }
        return new OfficeUnit
        {
            Id = id,
            BuildingId = buildingId,
            Floor = floor,
            Name = name,
            PriceRappen = priceRappen,
            AreaSquareMeters = areaSquareMeters,
        };
    }

    /// <summary>Keeps name and price in line with the current offer (only while nobody owns it).</summary>
    public void UpdateOffer(string name, long priceRappen, int areaSquareMeters)
    {
        Name = name;
        AreaSquareMeters = areaSquareMeters;
        if (IsAvailable)
        {
            PriceRappen = priceRappen;
        }
    }
}

/// <summary>
/// Offices for sale in the Prime Tower: two per office storey (2nd–33rd, except the public floors 12 and 24).
/// Higher = more expensive; the south side (lake view) costs a bit more. With the starting capital of
/// CHF 10'000 most of them are affordable, the top ones need savings (economy system).
/// </summary>
internal static class PrimeTowerOffices
{
    public static readonly int[] PublicFloors = [0, 12, 24, 34, 35];

    public static IEnumerable<(Guid Id, int Floor, string Name, long PriceRappen, int Area)> Offer()
    {
        for (var floor = 2; floor <= 33; floor++)
        {
            if (PublicFloors.Contains(floor))
            {
                continue;
            }
            var basePrice = 2_500m + floor * 250m;
            yield return (StableId(floor, 1), floor, $"Büro {floor}.1 · Nord", (long)(basePrice * 100m), 160);
            yield return (StableId(floor, 2), floor, $"Büro {floor}.2 · Süd, Seesicht", (long)((basePrice + 750m) * 100m), 190);
        }
    }

    /// <summary>Stable ids, so re-seeding updates instead of duplicating.</summary>
    private static Guid StableId(int floor, int number) => new($"0199c000-0000-7000-8000-{floor:D6}{number:D6}");
}
