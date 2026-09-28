using System;
using System.Collections.Generic;
using System.Linq;

namespace Reconnect.Contracts.Avatars
{
    /// <summary>German names of the wardrobe's parts, skins and eyes, and the dressing rules of the character creator.</summary>
    public static partial class Wardrobe
    {
        private static readonly Dictionary<string, string> PartNames = new Dictionary<string, string>
        {
            // Hair.
            ["afro01"] = "Afro", ["bob01"] = "Bob klassisch", ["bob02"] = "Bob fransig", ["braid01"] = "Zopf",
            ["cortu_shaggy_green_hair"] = "Shag", ["cortu_short_messy_hair"] = "Kurz verwuschelt", ["cortu_straight_bangs"] = "Glatt mit Pony",
            ["cortu_strawberry_cloud_hair"] = "Lockenwolke", ["culturalibre_hair_01"] = "Wellen", ["culturalibre_hair_02"] = "Seitenscheitel",
            ["culturalibre_hair_05"] = "Undercut", ["culturalibre_hair_06"] = "Lang gestuft", ["elvs_double_mh_braid"] = "Doppelzopf",
            ["elvs_french_braid_variation"] = "Französischer Zopf", ["elvs_reverse_french_braid_bun"] = "Zopf-Dutt",
            ["elvs_unkempt_french_braid"] = "Lockerer Flechtzopf", ["faydaen_hair_1"] = "Mittellang", ["littleright_bobcut_hair"] = "Kurzer Bob",
            ["long01"] = "Lang offen", ["o4saken_long01"] = "Lang glatt", ["ponytail01"] = "Pferdeschwanz", ["rehmanpolanski_hair_bun_brown"] = "Dutt",
            ["short01"] = "Kurz gescheitelt", ["short02"] = "Kurz klassisch", ["short03"] = "Kurz gestylt", ["short04"] = "Buzz Cut",
            ["sonntag78_blond_with_headband"] = "Mit Haarband", ["toigo_blunt_bob"] = "Stumpfer Bob", ["toigo_blunt_bob_with_bangs"] = "Stumpfer Bob mit Pony",
            ["toigo_curled_under_bob"] = "Eingedrehter Bob", ["toigo_curled_under_bob_with_bangs"] = "Eingedrehter Bob mit Pony",
            ["toigo_inverted_bob"] = "Long Bob", ["toigo_inverted_bob_with_bangs"] = "Long Bob mit Pony",
            // Beards.
            ["culturalibre_faun_beard"] = "Kinnbart", ["grinsegold_beard_sigmund_wip"] = "Vollbart", ["rehmanpolanski_beard_viking"] = "Wikingerbart",
            ["rehmanpolanski_moustache_viking"] = "Schnauz", ["wdg_scruffy_beard"] = "Dreitagebart",
            // Hats.
            ["fedora01"] = "Fedora", ["fedora_cocked"] = "Fedora schräg",
            // Shoes.
            ["cortu_floppy_overknee_shoes"] = "Overknee-Stiefel weich", ["cortu_t-bar"] = "Riemchenschuhe", ["culturalibre_hero_boots_1"] = "Schnürstiefel",
            ["culturalibre_hero_boots_2"] = "Bikerboots", ["culturalibre_hero_boots_3"] = "Hohe Stiefel", ["culturalibre_heroine_boots_1"] = "Stiefel mit Absatz",
            ["culturalibre_heroine_boots_2"] = "Overknee-Stiefel", ["culturalibre_male_boots"] = "Boots", ["grinsegold_female_pirate_boots"] = "Stulpenstiefel",
            ["scailman_gogo_platform_boots"] = "Plateau-Stiefel", ["shoes01"] = "Sneaker", ["shoes02"] = "Halbschuhe", ["shoes03"] = "Lederschuhe",
            ["shoes04"] = "Loafer", ["shoes05"] = "Turnschuhe", ["shoes06"] = "Pumps", ["toigo_ankle_boots_female"] = "Stiefeletten",
            ["toigo_ankle_boots_male"] = "Chelsea Boots", ["toigo_ballet_flats"] = "Ballerinas", ["toigo_ballet_flats_with_bows"] = "Ballerinas mit Schleife",
            ["toigo_ballet_flats_with_flowers"] = "Ballerinas mit Blüten", ["toigo_flats"] = "Slipper", ["toigo_mj_cloth_shoes"] = "Stoffschuhe",
            ["toigo_stiletto_booties"] = "Stiletto-Stiefeletten",
            // Brows.
            ["eyebrow001"] = "Kräftig", ["eyebrow002"] = "Gerade", ["eyebrow003"] = "Buschig", ["eyebrow005"] = "Fein", ["eyebrow008"] = "Geschwungen",
            ["eyebrow009"] = "Schmal", ["eyebrow010"] = "Natürlich", ["eyebrow011"] = "Hoch", ["eyebrow012"] = "Weich",
            // Dresses.
            ["aethelraed_flapper_dress"] = "Charleston-Kleid", ["mindfront_kimono"] = "Kimono", ["toigo_bodice_dress_with_lace_ruffle_skirt"] = "Korsagenkleid mit Spitze",
            ["toigo_camisole_dress_with_full_skirt"] = "Trägerkleid schwingend", ["toigo_cut_out_dress"] = "Cut-out-Kleid",
            ["toigo_dress_with_tiered_skirt"] = "Stufenkleid", ["toigo_halter_dress_knee_length"] = "Neckholder-Kleid knielang",
            ["toigo_halter_dress_midi"] = "Neckholder-Kleid midi", ["toigo_halter_dress_with_fluted_skirt"] = "Neckholder-Kleid ausgestellt",
            ["toigo_keyhole_neck_dress"] = "Schlüssellochkleid", ["toigo_shift_dress"] = "Etuikleid", ["toigo_strapless_ruffle_top_dress"] = "Trägerloses Rüschenkleid",
            // Bottoms.
            ["cortu_cargo_pants"] = "Cargohose", ["cortu_jeans_shorts"] = "Jeansshorts", ["frankyaye_mini_skirt_01"] = "Minirock",
            ["frankyaye_mini_skirt_02"] = "Minirock gerafft", ["toigo_harem_pants"] = "Pumphose", ["toigo_long_full_skirt"] = "Maxirock",
            ["toigo_skirt_with_lace_ruffle"] = "Rock mit Spitze", ["toigo_tiered_mini_skirt"] = "Stufen-Minirock", ["toigo_tiered_skirt"] = "Stufenrock",
            ["toigo_wool_pants"] = "Stoffhose",
            // Tops.
            ["elvs_crude_t-shirt_male"] = "T-Shirt weit", ["joepal_crude_t-shirt_female"] = "T-Shirt locker", ["namuhekam_male_polo_shirt"] = "Poloshirt",
            ["skalldyrssuppe_tube_top_funky_colors"] = "Bandeau-Top", ["toigo_basic_tucked_t-shirt"] = "T-Shirt", ["toigo_bodice-style_top"] = "Korsagen-Top",
            ["toigo_camisole_top"] = "Spaghettitop", ["toigo_fisherman_sweater"] = "Strickpullover", ["toigo_keyhole_tank_top"] = "Tanktop",
            ["toigo_turtleneck_halter_top"] = "Rollkragen-Top",
            // Outfits.
            ["female_casualsuit01"] = "Freizeit-Look 1", ["female_casualsuit02"] = "Freizeit-Look 2", ["female_elegantsuit01"] = "Elegant",
            ["female_sportsuit01"] = "Sport", ["male_casualsuit01"] = "Freizeit-Look 1", ["male_casualsuit02"] = "Freizeit-Look 2",
            ["male_casualsuit03"] = "Freizeit-Look 3", ["male_casualsuit04"] = "Freizeit-Look 4", ["male_casualsuit05"] = "Freizeit-Look 5",
            ["male_casualsuit06"] = "Freizeit-Look 6", ["male_elegantsuit01"] = "Anzug elegant", ["male_worksuit01"] = "Arbeitskleidung",
            ["toigo_female_double-breasted_suit"] = "Zweireiher", ["toigo_female_suit"] = "Hosenanzug", ["toigo_female_suit_2"] = "Hosenanzug 2",
            ["toigo_male_double-breasted_suit"] = "Zweireiher", ["toigo_male_suit_3"] = "Business-Anzug", ["toigo_male_suit_tie_and_jacket"] = "Anzug mit Krawatte",
            ["toigo_suit_with_dinner_jacket"] = "Dinnerjacket", ["toigo_suit_with_jacket_and_bowtie"] = "Smoking",
        };

        private static readonly Dictionary<string, string> EyeNames = new Dictionary<string, string>
        {
            ["brown"] = "Braun", ["brownlight"] = "Hellbraun", ["green"] = "Grün", ["bluegreen"] = "Blaugrün", ["blue"] = "Blau",
            ["lightblue"] = "Hellblau", ["deepblue"] = "Tiefblau", ["grey"] = "Grau", ["ice"] = "Eisblau",
        };

        /// <summary>Tints that suit hair (the others are for clothes).</summary>
        public static readonly string[] HairTints =
            { "black", "espresso", "chestnut", "caramel", "honey", "blonde", "platinum", "auburn", "copper", "silver" };

        /// <summary>Tints that suit clothes, hats and shoes.</summary>
        public static readonly string[] FashionTints =
        {
            "white", "cream", "sand", "camel", "grey", "charcoal", "black", "navy", "denim", "sky", "teal", "sage", "olive", "emerald",
            "burgundy", "red", "coral", "blush", "pink", "lilac", "mustard",
        };

        /// <summary>The name of a part (German), or a tidy version of its id.</summary>
        public static string PartName(string id) =>
            id != null && PartNames.TryGetValue(id, out var name) ? name : (id ?? "").Replace('_', ' ');

        public static string EyeName(string id) => id != null && EyeNames.TryGetValue(id, out var name) ? name : id;

        /// <summary>Skin tones are numbered from light to dark (the order of <see cref="Skins"/>).</summary>
        public static string SkinName(string body, string skin) =>
            Skins.TryGetValue(body ?? "", out var skins) && Array.IndexOf(skins, skin) is var index and >= 0 ? $"Ton {index + 1}" : skin;

        /// <summary>The ids of a kind the body can wear, in the order of the wardrobe.</summary>
        public static IReadOnlyList<string> PartsOf(string body, string kind) =>
            Parts.TryGetValue(body ?? "", out var parts) ? parts.Where(p => p.Value.Kind == kind).Select(p => p.Key).ToList() : new List<string>();

        public static string KindOf(string body, string id) =>
            Parts.TryGetValue(body ?? "", out var parts) && parts.TryGetValue(id ?? "", out var part) ? part.Kind : null;

        /// <summary>Kinds that can be taken off without leaving the figure undressed.</summary>
        public static bool IsOptional(string kind) => kind == Hat || kind == Beard || kind == Hair || kind == Shoes;

        /// <summary>
        /// Puts on a part: it replaces the part of its kind; a dress or outfit replaces top and bottom (and the other one),
        /// a top or bottom replaces a dress or outfit – the missing half then comes from the start look. Keeps the tint of the
        /// replaced part when it fits the new one.
        /// </summary>
        public static AvatarLookDto Wear(AvatarLookDto look, string id, string variant = "default", string tint = null)
        {
            var kind = KindOf(look.Body, id);
            if (kind == null)
            {
                return look;
            }
            if (kind == Brows)
            {
                return look with { Brows = id };
            }
            var fullBody = kind == Dress || kind == Outfit;
            var parts = (look.Parts ?? Array.Empty<AvatarPartDto>()).ToList();
            var replaced = parts.FirstOrDefault(p => KindOf(look.Body, p.Id) == kind);
            parts.RemoveAll(p =>
            {
                var other = KindOf(look.Body, p.Id);
                return other == kind
                       || (fullBody && (other == Top || other == Bottom || other == Dress || other == Outfit))
                       || ((kind == Top || kind == Bottom) && (other == Dress || other == Outfit));
            });
            parts.Add(new AvatarPartDto(id, variant ?? "default", tint ?? replaced?.Tint));
            if (kind == Top || kind == Bottom)
            {
                var missing = kind == Top ? Bottom : Top;
                if (parts.All(p => KindOf(look.Body, p.Id) != missing))
                {
                    var start = Default(Male).Parts.First(p => KindOf(Male, p.Id) == missing);
                    parts.Add(start);
                }
            }
            return look with { Parts = parts };
        }

        /// <summary>Takes off the part of an optional kind (hat, beard, hair, shoes).</summary>
        public static AvatarLookDto TakeOff(AvatarLookDto look, string kind) =>
            !IsOptional(kind) ? look : look with { Parts = look.Parts.Where(p => KindOf(look.Body, p.Id) != kind).ToList() };

        /// <summary>Changes the colour (tint, or null for the original colours) of the worn part of a kind.</summary>
        public static AvatarLookDto Tint(AvatarLookDto look, string kind, string tint) =>
            look with { Parts = look.Parts.Select(p => KindOf(look.Body, p.Id) == kind ? p with { Tint = tint } : p).ToList() };

        public static AvatarPartDto Worn(AvatarLookDto look, string kind) => look.Parts?.FirstOrDefault(p => KindOf(look.Body, p.Id) == kind);

        /// <summary>A random, complete and valid look for the body ("Zufall").</summary>
        public static AvatarLookDto Random(string body, Random random)
        {
            T Pick<T>(IReadOnlyList<T> list) => list[random.Next(list.Count)];
            string Of(string kind) => Pick(PartsOf(body, kind));
            var look = Default(body) with
            {
                Skin = Pick(Skins[body]),
                Eyes = Pick(Eyes),
                Brows = Of(Brows),
                WalkStyle = Pick(WalkStyles.Keys.ToList()),
                Height = (float)Math.Round(MinHeight + random.NextDouble() * (MaxHeight - MinHeight), 2),
                Parts = new List<AvatarPartDto>(),
            };
            var hairTint = random.Next(3) == 0 ? null : Pick(HairTints);
            look = Wear(look, Of(Hair), tint: hairTint);
            var clothes = body == Female ? random.Next(3) : random.Next(2) + 1;   // women: dress, outfit or separates
            if (clothes == 0 && PartsOf(body, Dress).Count > 0)
            {
                look = Wear(look, Of(Dress));
            }
            else if (clothes == 1)
            {
                look = Wear(look, Of(Outfit));
            }
            else
            {
                look = Wear(Wear(look, Of(Top), tint: random.Next(2) == 0 ? Pick(FashionTints) : null), Of(Bottom),
                    tint: random.Next(2) == 0 ? Pick(FashionTints) : null);
            }
            look = Wear(look, Of(Shoes));
            if (body == Male && random.Next(2) == 0)
            {
                look = Wear(look, Of(Beard), tint: hairTint);
            }
            if (random.Next(6) == 0)
            {
                look = Wear(look, Of(Hat));
            }
            return look;
        }
    }
}
