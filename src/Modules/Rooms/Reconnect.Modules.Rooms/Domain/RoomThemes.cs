using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.Rooms.Domain;

/// <summary>Visual theme of a room (floor, walls, light). The client maps the id to its look.</summary>
internal static class RoomThemes
{
    public const string Cozy = "cozy";
    public const string Rooftop = "rooftop";
    public const string Cafe = "cafe";
    public const string Atelier = "atelier";
    public const string Opera = "opera";
    public const string Library = "library";
    public const string SkyLounge = "skylounge";

    public const int MaxLength = 32;

    public static readonly IReadOnlyList<string> All = [Cozy, Rooftop, Cafe, Atelier, Opera, Library, SkyLounge];

    public static string Validate(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return Cozy;
        }
        theme = theme.Trim().ToLowerInvariant();
        return All.Contains(theme) ? theme : throw new DomainException($"Unknown room theme '{theme}'.");
    }
}
