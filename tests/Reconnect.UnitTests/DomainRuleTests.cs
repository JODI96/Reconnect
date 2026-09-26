using Reconnect.Modules.Profiles.Domain;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Safety.Domain;
using Reconnect.Modules.Social.Domain;
using Reconnect.SharedKernel.Domain;

namespace Reconnect.UnitTests;

/// <summary>Business rules of the module domains – fast, no database.</summary>
public sealed class DomainRuleTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.CreateVersion7();
    private static readonly Guid Ben = Guid.CreateVersion7();

    [Theory]
    [InlineData(2008, 9, 26, true)]    // 18th birthday today
    [InlineData(2008, 9, 27, false)]   // turns 18 tomorrow
    [InlineData(1990, 1, 1, true)]
    public void Only_adults_may_have_a_profile(int year, int month, int day, bool allowed)
    {
        var birthDate = new DateOnly(year, month, day);

        Assert.Equal(allowed, AgePolicy.IsAdult(birthDate, Today));
        if (allowed)
        {
            Assert.Equal(birthDate, Profile.Create(Anna, "Anna", birthDate, Today).BirthDate);
        }
        else
        {
            Assert.Throws<DomainException>(() => Profile.Create(Anna, "Anna", birthDate, Today));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Profile_needs_a_display_name(string name) =>
        Assert.Throws<DomainException>(() => Profile.Create(Anna, name, new DateOnly(1995, 1, 1), Today));

    [Fact]
    public void Profile_trims_name_and_drops_empty_bio()
    {
        var profile = Profile.Create(Anna, "  Anna  ", new DateOnly(1995, 1, 1), Today);
        profile.Update(" Anna B. ", "   ");

        Assert.Equal("Anna B.", profile.DisplayName);
        Assert.Null(profile.Bio);
        Assert.Equal(31, profile.AgeOn(Today));
    }

    [Fact]
    public void Users_cannot_block_like_or_report_themselves()
    {
        Assert.Throws<DomainException>(() => Block.Create(Anna, Anna, Now));
        Assert.Throws<DomainException>(() => Like.Create(Anna, Anna, Now));
        Assert.Throws<DomainException>(() => Report.Create(Anna, Anna, ReportReason.Spam, null, null, null, Now));
    }

    [Fact]
    public void Match_stores_the_pair_ordered_so_it_exists_only_once()
    {
        var ab = Match.Create(Anna, Ben, Now);
        var ba = Match.Create(Ben, Anna, Now);

        Assert.Equal((ab.User1Id, ab.User2Id), (ba.User1Id, ba.User2Id));
        Assert.True(ab.User1Id.CompareTo(ab.User2Id) < 0);
        Assert.Equal(Ben, ab.OtherUser(Anna));
        Assert.Equal(Anna, ab.OtherUser(Ben));
    }

    [Fact]
    public void Only_match_participants_can_send_messages()
    {
        var match = Match.Create(Anna, Ben, Now);

        Assert.Throws<DomainException>(() => Message.Create(match, Guid.CreateVersion7(), "Hallo", Now));
        Assert.Throws<DomainException>(() => Message.Create(match, Anna, "   ", Now));
        Assert.Equal("Hallo", Message.Create(match, Anna, " Hallo ", Now).Text);
    }

    [Fact]
    public void Report_validates_reason_and_comment_length()
    {
        Assert.Throws<DomainException>(() => Report.Create(Anna, Ben, (ReportReason)99, null, null, null, Now));
        Assert.Throws<DomainException>(() =>
            Report.Create(Anna, Ben, ReportReason.Other, new string('x', Report.CommentMaxLength + 1), null, null, Now));

        var report = Report.Create(Anna, Ben, ReportReason.Harassment, "  ", null, null, Now);
        Assert.Null(report.Comment);
        Assert.Equal(ReportStatus.Open, report.Status);
    }

    [Theory]
    [InlineData(Room.MinSize - 1, 10)]
    [InlineData(10, Room.MaxSize + 1)]
    public void Room_size_is_limited(int width, int depth)
    {
        var room = Room.Create(Anna, Guid.CreateVersion7(), "Loft", isPublic: true);

        Assert.Throws<DomainException>(() => room.Resize(width, depth));
    }

    [Fact]
    public void Room_theme_defaults_to_cozy_and_rejects_unknown_themes()
    {
        Assert.Equal(RoomThemes.Cozy, Room.Create(Anna, Guid.CreateVersion7(), "Loft", true).Theme);
        Assert.Equal(RoomThemes.Rooftop, Room.Create(Anna, Guid.CreateVersion7(), "Loft", true, " ROOFTOP ").Theme);
        Assert.Throws<DomainException>(() => Room.Create(Anna, Guid.CreateVersion7(), "Loft", true, "disco"));
    }

    [Fact]
    public void Private_rooms_are_only_visible_to_the_owner()
    {
        var room = Room.Create(Anna, Guid.CreateVersion7(), "Privat", isPublic: false);

        Assert.True(room.IsVisibleTo(Anna));
        Assert.False(room.IsVisibleTo(Ben));
    }

    [Fact]
    public void Room_layout_is_limited_and_needs_item_ids()
    {
        var room = Room.Create(Anna, Guid.CreateVersion7(), "Loft", isPublic: true);
        RoomItem Item(string id) => new() { ItemId = id, Position = new Position3() };

        Assert.Throws<DomainException>(() => room.ReplaceLayout(Enumerable.Range(0, Room.MaxItems + 1).Select(_ => Item("chair"))));
        Assert.Throws<DomainException>(() => room.ReplaceLayout([Item(" ")]));

        room.ReplaceLayout([Item("chair"), Item("table")]);
        Assert.Equal(2, room.Layout.Count);
    }
}
