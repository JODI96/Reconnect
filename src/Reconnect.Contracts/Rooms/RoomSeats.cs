using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>
    /// Furniture people can sit on and how many places it has (a sofa seats several). Shared by server (validates
    /// <c>Sit</c>) and client (places the avatar; seat height and facing come from the model's shape).
    /// </summary>
    public static class RoomSeats
    {
        private static readonly Dictionary<string, int> Places = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Kenney furniture kit
            ["chair"] = 1, ["chairCushion"] = 1, ["chairDesk"] = 1, ["chairRounded"] = 1, ["chairModernCushion"] = 1,
            ["chairModernFrameCushion"] = 1, ["stoolBar"] = 1, ["loungeChair"] = 1, ["loungeDesignChair"] = 1,
            ["benchCushion"] = 2, ["loungeSofa"] = 2, ["loungeDesignSofa"] = 2,
            // Poly Haven (ph-...)
            ["ph-dining_chair_02"] = 1, ["ph-bar_chair_round_01"] = 1, ["ph-GreenChair_01"] = 1, ["ph-ArmChair_01"] = 1,
            ["ph-modern_arm_chair_01"] = 1, ["ph-mid_century_lounge_chair"] = 1, ["ph-Ottoman_01"] = 1,
            ["ph-sofa_02"] = 3, ["ph-sofa_03"] = 2,
            // Built by the client
            ["custom-lounger"] = 1,
        };

        /// <summary>Places on the item (0 = not a seat).</summary>
        public static int PlacesFor(string itemId) =>
            itemId != null && Places.TryGetValue(itemId, out var places) ? places : 0;

        public static bool IsSeat(string itemId) => PlacesFor(itemId) > 0;
    }

    /// <summary>A place to sit: index of the item in the room layout and the place on it (0 … places-1).</summary>
    public sealed record SeatDto(int Item, int Place);

    /// <summary>Someone sat down (Seat) or stood up (Seat = null).</summary>
    public sealed record PlayerSeatDto(Guid UserId, SeatDto? Seat);
}
