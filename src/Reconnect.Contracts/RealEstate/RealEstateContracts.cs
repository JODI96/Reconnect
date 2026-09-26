using System;

namespace Reconnect.Contracts.RealEstate
{
    /// <summary>A private office that can be bought (Prime Tower first, later every building).</summary>
    /// <param name="Price">Swiss francs.</param>
    /// <param name="RoomId">The owner's room on that floor (only for the owner).</param>
    public sealed record OfficeUnitDto(
        Guid Id,
        Guid BuildingId,
        int Floor,
        string Name,
        decimal Price,
        int AreaSquareMeters,
        bool IsAvailable,
        bool IsMine,
        Guid? RoomId);

    /// <param name="Balance">Wallet balance after the purchase/sale (CHF).</param>
    public sealed record OfficeTransactionDto(OfficeUnitDto Unit, decimal Balance);
}
