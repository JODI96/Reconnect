using Microsoft.EntityFrameworkCore;
using Reconnect.Domain.Rooms;
using Reconnect.Infrastructure.Persistence;
using Reconnect.Infrastructure.Persistence.Seed;

namespace Reconnect.Api.Features.Showcase;

/// <summary>
/// DEVELOPMENT ONLY: five fully furnished public rooms owned by the dev admin, so the app has
/// something to explore. Runs after the dev admin exists; re-running updates layout and theme.
/// Coordinates: 10×10 tiles, (0,0) is the front corner the camera looks from, back walls at z=10 and x=10.
/// Item ids = Kenney Furniture Kit model names; "game-*" items are interactive minigame stations.
/// </summary>
public static partial class ShowcaseRooms
{
    // Kenney models face -Z ("south", towards the camera side) at rotation 0.
    private const float South = 0f;
    private const float West = 90f;
    private const float North = 180f;
    private const float East = 270f;

    public static async Task SeedShowcaseRoomsIfEnabledAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Showcase:Enabled"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReconnectDbContext>();
        var adminName = app.Configuration["DevAdmin:UserName"] ?? "Admin";
        var admin = await db.Users.SingleOrDefaultAsync(u => u.UserName == adminName);
        if (admin is null)
        {
            return;   // DevAdmin disabled – nobody to own the rooms
        }

        foreach (var (name, buildingId, theme, layout) in Definitions())
        {
            var room = await db.Rooms.SingleOrDefaultAsync(r => r.OwnerId == admin.Id && r.Name == name);
            if (room is null)
            {
                room = Room.Create(admin.Id, buildingId, name, isPublic: true, theme);
                db.Rooms.Add(room);
            }
            room.ChangeTheme(theme);
            room.ReplaceLayout(layout);
        }
        await db.SaveChangesAsync();
        LogSeeded(app.Logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV ONLY: showcase rooms are up to date.")]
    private static partial void LogSeeded(ILogger logger);

    private static IEnumerable<(string Name, Guid BuildingId, string Theme, List<RoomItem> Layout)> Definitions()
    {
        yield return ("Rooftop Lounge", ZurichBuildings.PrimeTowerId, RoomThemes.Rooftop,
        [
            Item("loungeSofaLong", 5f, 9.45f, South),
            Item("loungeSofa", 9.45f, 6.2f, West),
            Item("tableCoffeeGlass", 5f, 8.2f),
            Item("rugRound", 5.4f, 7.6f),
            Item("pottedPlant", 0.6f, 9.4f), Item("pottedPlant", 9.4f, 9.4f), Item("pottedPlant", 9.4f, 0.6f),
            Item("lampRoundFloor", 2.9f, 9.5f), Item("lampRoundFloor", 9.5f, 4.3f),
            Item("speaker", 1.6f, 9.6f), Item("speaker", 7.4f, 9.6f),
            Item("kitchenBar", 1.2f, 5.4f, East), Item("kitchenBar", 1.2f, 6.4f, East),
            Item("stoolBar", 2.2f, 5.4f), Item("stoolBar", 2.2f, 6.4f),
            Item("game-tictactoe", 5.5f, 4.5f),
            Item("chairCushion", 4.5f, 4.5f, East), Item("chairCushion", 6.5f, 4.5f, West),
        ]);

        yield return ("Café Limmat", ZurichBuildings.GrossmuensterId, RoomThemes.Cafe,
        [
            Item("kitchenFridgeLarge", 5.3f, 9.6f, South),
            Item("kitchenBar", 6.5f, 9.6f, South), Item("kitchenBar", 7.5f, 9.6f, South), Item("kitchenBarEnd", 8.5f, 9.6f, South),
            Item("stoolBar", 6.5f, 8.6f), Item("stoolBar", 7.5f, 8.6f),
            Item("rugRectangle", 2.5f, 5.2f, West),
            Item("tableRound", 2.5f, 7f), Item("chairRounded", 1.6f, 7f, East), Item("chairRounded", 3.4f, 7f, West),
            Item("tableRound", 2.5f, 3.5f), Item("chairRounded", 1.6f, 3.5f, East), Item("chairRounded", 3.4f, 3.5f, West),
            Item("tableRound", 6.2f, 5.5f), Item("chairRounded", 5.3f, 5.5f, East), Item("chairRounded", 7.1f, 5.5f, West),
            Item("game-quiz", 9.6f, 5.5f, West),
            Item("pottedPlant", 0.6f, 9.4f), Item("pottedPlant", 9.4f, 9.4f), Item("plantSmall2", 9.5f, 1f),
            Item("lampRoundFloor", 0.6f, 5.2f),
        ]);

        yield return ("Kunst-Atelier", ZurichBuildings.KunsthausId, RoomThemes.Atelier,
        [
            Item("desk", 8.4f, 9.45f, South), Item("chairDesk", 8.4f, 8.5f, North),
            Item("bookcaseOpen", 6.1f, 9.7f, South), Item("bookcaseOpenLow", 4.2f, 9.7f, South),
            Item("lampSquareFloor", 9.5f, 7.4f),
            Item("rugSquare", 4.5f, 5f),
            Item("tableCross", 3f, 6.3f), Item("chairModernCushion", 3f, 7.3f, South), Item("chairModernCushion", 3f, 5.3f, North),
            Item("sideTable", 9.6f, 3f),
            Item("cardboardBoxClosed", 9.4f, 1f), Item("cardboardBoxOpen", 8.6f, 0.8f),
            Item("bear", 0.8f, 6f, East),
            Item("pottedPlant", 0.6f, 9.4f),
            Item("game-tictactoe", 6.2f, 3.5f),
            Item("chairModernFrameCushion", 5.2f, 3.5f, East), Item("chairModernFrameCushion", 7.2f, 3.5f, West),
        ]);

        yield return ("Opern-Foyer", ZurichBuildings.OpernhausId, RoomThemes.Opera,
        [
            Item("loungeDesignSofa", 3.8f, 9.45f, South), Item("loungeDesignSofa", 7f, 9.45f, South),
            Item("loungeDesignChair", 9.45f, 7f, West), Item("loungeDesignChair", 9.45f, 5.4f, West),
            Item("tableCoffeeGlassSquare", 5.4f, 8.1f),
            Item("rugRounded", 5.4f, 7.7f),
            Item("rugSquare", 4f, 3.6f),   // dance floor
            Item("pottedPlant", 0.6f, 9.4f), Item("pottedPlant", 9.4f, 9.4f), Item("pottedPlant", 9.4f, 0.6f), Item("pottedPlant", 0.6f, 0.6f),
            Item("lampRoundFloor", 2.2f, 9.6f), Item("lampRoundFloor", 9.6f, 8.6f), Item("lampRoundFloor", 9.6f, 3.8f),
            Item("speaker", 0.8f, 5f, East), Item("speakerSmall", 7.2f, 2.4f, West),
            Item("coatRackStanding", 0.7f, 2.6f),
            Item("benchCushion", 3f, 0.7f),
        ]);

        yield return ("ETH Bibliothek", ZurichBuildings.EthHauptgebaeudeId, RoomThemes.Library,
        [
            Item("bookcaseClosedWide", 2.2f, 9.7f, South), Item("bookcaseClosedWide", 4.6f, 9.7f, South), Item("bookcaseClosedWide", 7f, 9.7f, South),
            Item("bookcaseClosed", 9.7f, 8.2f, West), Item("bookcaseClosed", 9.7f, 7f, West),
            Item("rugRectangle", 4.8f, 6f),
            Item("desk", 3f, 6.2f), Item("chairDesk", 3f, 5.2f, North),
            Item("desk", 6.5f, 6.2f), Item("chairDesk", 6.5f, 5.2f, North),
            Item("loungeChairRelax", 8.6f, 2.4f, West), Item("sideTable", 9.5f, 1.2f), Item("lampRoundFloor", 9.6f, 3.4f),
            Item("game-quiz", 9.6f, 5f, West),
            Item("pottedPlant", 0.6f, 9.4f), Item("books", 0.8f, 7f),
        ]);
    }

    private static RoomItem Item(string itemId, float x, float z, float rotation = 0f) =>
        new() { ItemId = itemId, Position = new Position3 { X = x, Y = 0f, Z = z }, Rotation = rotation };
}
