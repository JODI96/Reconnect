using Reconnect.Contracts.Avatars;

namespace Reconnect.UnitTests.Avatars;

public sealed class WardrobeTests
{
    [Theory]
    [InlineData(Wardrobe.Female)]
    [InlineData(Wardrobe.Male)]
    public void The_start_looks_are_valid(string body) => Assert.Empty(Wardrobe.Problems(Wardrobe.Default(body)));

    [Fact]
    public void Both_bodies_have_plenty_to_choose_from()
    {
        foreach (var body in new[] { Wardrobe.Female, Wardrobe.Male })
        {
            var parts = Wardrobe.Parts[body].Values.ToList();
            Assert.True(parts.Count(p => p.Kind == Wardrobe.Hair) >= 20, $"{body}: hairstyles");
            Assert.True(parts.Count(p => p.Kind is Wardrobe.Outfit or Wardrobe.Dress or Wardrobe.Top) >= 10, $"{body}: clothes");
            Assert.True(parts.Count(p => p.Kind == Wardrobe.Shoes) >= 8, $"{body}: shoes");
            Assert.True(Wardrobe.Skins[body].Length >= 10, $"{body}: skin tones");
        }
        Assert.Contains(Wardrobe.Parts[Wardrobe.Male].Values, p => p.Kind == Wardrobe.Beard);
    }

    [Fact]
    public void A_dress_or_suit_takes_the_place_of_top_and_trousers()
    {
        var look = Wardrobe.Default(Wardrobe.Female) with
        {
            Parts = [new AvatarPartDto("toigo_shift_dress"), new AvatarPartDto("toigo_basic_tucked_t-shirt")],
        };

        Assert.Contains(Wardrobe.Problems(look), p => p.Contains("ersetzt"));
    }

    [Fact]
    public void Parts_of_the_other_body_unknown_tints_and_odd_heights_are_refused()
    {
        var male = Wardrobe.Default(Wardrobe.Male);

        Assert.NotEmpty(Wardrobe.Problems(male with { Parts = [new AvatarPartDto("toigo_shift_dress")] }));   // women's dress
        Assert.NotEmpty(Wardrobe.Problems(male with { Parts = [new AvatarPartDto("short02", Tint: "neon")] }));
        Assert.NotEmpty(Wardrobe.Problems(male with { Height = 1.4f }));
        Assert.NotEmpty(Wardrobe.Problems(male with { WalkStyle = "moonwalk" }));
        Assert.NotEmpty(Wardrobe.Problems(male with { Parts = [new AvatarPartDto("short02"), new AvatarPartDto("short04")] }));
    }

    [Fact]
    public void Tints_are_colours()
    {
        var (r, g, b) = Wardrobe.TintColour("navy");
        Assert.InRange(r, 0f, 0.3f);
        Assert.InRange(b, 0.2f, 0.4f);
        Assert.All(Wardrobe.Tints.Values, hex => Assert.Matches("^[0-9A-F]{6}$", hex));
    }

    [Fact]
    public void Putting_on_a_dress_takes_off_top_and_trousers_and_a_top_brings_trousers_back()
    {
        var male = Wardrobe.Default(Wardrobe.Male);
        var suit = Wardrobe.Wear(male, "male_elegantsuit01");
        Assert.Empty(Wardrobe.Problems(suit));
        Assert.Null(Wardrobe.Worn(suit, Wardrobe.Top));
        Assert.Null(Wardrobe.Worn(suit, Wardrobe.Bottom));

        var polo = Wardrobe.Wear(suit, "namuhekam_male_polo_shirt", tint: "navy");
        Assert.Empty(Wardrobe.Problems(polo));
        Assert.Null(Wardrobe.Worn(polo, Wardrobe.Outfit));
        Assert.NotNull(Wardrobe.Worn(polo, Wardrobe.Bottom));   // not bare legs
        Assert.Equal("navy", Wardrobe.Worn(polo, Wardrobe.Top)!.Tint);

        var bald = Wardrobe.TakeOff(polo, Wardrobe.Hair);
        Assert.Null(Wardrobe.Worn(bald, Wardrobe.Hair));
        Assert.Same(bald, Wardrobe.TakeOff(bald, Wardrobe.Top));   // clothes stay on
    }

    [Fact]
    public void Random_looks_are_always_valid_and_dressed()
    {
        var random = new Random(7);
        for (var i = 0; i < 300; i++)
        {
            var look = Wardrobe.Random(i % 2 == 0 ? Wardrobe.Female : Wardrobe.Male, random);
            Assert.Empty(Wardrobe.Problems(look));
            Assert.True(Wardrobe.Worn(look, Wardrobe.Dress) != null || Wardrobe.Worn(look, Wardrobe.Outfit) != null
                        || (Wardrobe.Worn(look, Wardrobe.Top) != null && Wardrobe.Worn(look, Wardrobe.Bottom) != null));
        }
    }

    [Fact]
    public void Height_in_centimetres_round_trips_and_stays_in_range()
    {
        var man = Wardrobe.Default(Wardrobe.Male);
        Assert.Equal(189, Wardrobe.HeightCm(man));
        var (min, max) = Wardrobe.HeightRangeCm(Wardrobe.Male);
        for (var cm = min; cm <= max; cm++)
        {
            var look = Wardrobe.WithHeightCm(man, cm);
            Assert.Equal(cm, Wardrobe.HeightCm(look));
            Assert.Empty(Wardrobe.Problems(look));
        }
        Assert.Equal(max, Wardrobe.HeightCm(Wardrobe.WithHeightCm(man, 250)));
    }

    [Fact]
    public void Every_part_has_a_german_name()
    {
        foreach (var (id, _) in Wardrobe.Parts.Values.SelectMany(p => p))
        {
            Assert.NotEqual(id.Replace('_', ' '), Wardrobe.PartName(id));
        }
        Assert.All(Wardrobe.Eyes, e => Assert.NotEqual(e, Wardrobe.EyeName(e)));
    }
}
