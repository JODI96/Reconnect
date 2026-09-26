using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.Rooms.Domain;

internal sealed class Room : IAuditable
{
    public const int NameMaxLength = 80;
    public const int MaxItems = 800;
    public const int MinSize = 6;
    public const int MaxSize = 40;
    public const int DefaultSize = 10;
    public const int MinCapacity = 1;
    public const int MaxCapacity = 200;
    public const int DefaultCapacity = 25;
    public const int MaxFloor = 200;

    private Room() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid BuildingId { get; private set; }
    public string Name { get; private set; } = "";
    public bool IsPublic { get; private set; }
    public string Theme { get; private set; } = RoomThemes.Cozy;

    /// <summary>Floor size in 1 m tiles (x = width, z = depth).</summary>
    public int Width { get; private set; } = DefaultSize;
    public int Depth { get; private set; } = DefaultSize;

    /// <summary>Storey in a tower (0 = ground floor / lobby); null for ordinary rooms.</summary>
    public int? Floor { get; private set; }

    /// <summary>How many people may be in the room at once (enforced by the room hub, with a lift queue for towers).</summary>
    public int Capacity { get; private set; } = DefaultCapacity;

    /// <summary>Furniture layout, stored as jsonb.</summary>
    public List<RoomItem> Layout { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static Room Create(Guid ownerId, Guid buildingId, string name, bool isPublic, string? theme = null)
    {
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            BuildingId = buildingId,
            IsPublic = isPublic,
            Theme = RoomThemes.Validate(theme),
        };
        room.Rename(name);
        return room;
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    /// <summary>Puts the room on a storey of its building with a maximum number of people.</summary>
    public void PlaceOnFloor(int? floor, int capacity)
    {
        if (floor is < 0 or > MaxFloor)
        {
            throw new DomainException($"Floor must be 0-{MaxFloor}.");
        }
        if (capacity is < MinCapacity or > MaxCapacity)
        {
            throw new DomainException($"Capacity must be {MinCapacity}-{MaxCapacity}.");
        }
        Floor = floor;
        Capacity = capacity;
    }

    public void ChangeTheme(string theme) => Theme = RoomThemes.Validate(theme);

    public void Resize(int width, int depth)
    {
        if (width is < MinSize or > MaxSize || depth is < MinSize or > MaxSize)
        {
            throw new DomainException($"Room size must be {MinSize}–{MaxSize} tiles per side.");
        }
        Width = width;
        Depth = depth;
    }

    /// <summary>A room is visible to its owner, and to everyone else only if it is public.</summary>
    public bool IsVisibleTo(Guid userId) => IsPublic || IsOwnedBy(userId);

    public void Rename(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            throw new DomainException($"Room name must be 1-{NameMaxLength} characters.");
        }
        Name = name;
    }

    /// <summary>Replaces the whole layout. Callers must check <see cref="IsOwnedBy"/> first.</summary>
    public void ReplaceLayout(IEnumerable<RoomItem> items)
    {
        var list = items.ToList();
        if (list.Count > MaxItems)
        {
            throw new DomainException($"A room can contain at most {MaxItems} items.");
        }
        if (list.Any(i => string.IsNullOrWhiteSpace(i.ItemId) || i.ItemId.Length > RoomItem.ItemIdMaxLength))
        {
            throw new DomainException($"Every item needs an ItemId of 1-{RoomItem.ItemIdMaxLength} characters.");
        }

        Layout = list;
    }
}
