using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.Rooms.Domain;

internal sealed class Room : IAuditable
{
    public const int NameMaxLength = 80;
    public const int MaxItems = 800;
    public const int MinSize = 6;
    public const int MaxSize = 40;
    public const int DefaultSize = 10;

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
