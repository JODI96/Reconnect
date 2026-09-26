namespace Reconnect.Modules.Rooms.Public;

/// <summary>Creates and removes rooms on behalf of other modules (e.g. the office a user bought).</summary>
public interface IRoomProvisioning
{
    /// <summary>A private room of <paramref name="ownerId"/> on a storey of a building, furnished as a starter office.</summary>
    /// <returns>The new room's id.</returns>
    Task<Guid> CreateOfficeAsync(Guid ownerId, Guid buildingId, int floor, string name, int capacity, CancellationToken ct);

    /// <summary>Removes the room and its layout (e.g. the office was sold).</summary>
    Task DeleteRoomAsync(Guid roomId, CancellationToken ct);
}
