namespace Reconnect.Modules.Rooms.Domain;

/// <summary>
/// Public tower floors belong to the building itself, not to a user. Their owner id is a fixed id per
/// building (no account behind it); the owner name shown is the building's name.
/// </summary>
internal static class TowerOwners
{
    public static readonly Guid PrimeTower = new("0199b000-0000-7000-8000-000000000001");

    public static string? NameOf(Guid ownerId) => ownerId == PrimeTower ? "Prime Tower" : null;
}
