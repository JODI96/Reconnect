using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconnect.Domain.Social;
using Reconnect.Infrastructure.Identity;

namespace Reconnect.Infrastructure.Persistence.Configurations;

internal sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.HasIndex(l => new { l.FromUserId, l.ToUserId }).IsUnique();
        builder.HasIndex(l => l.ToUserId);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(l => l.FromUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(l => l.ToUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        // Pair is stored ordered (User1Id < User2Id), so this index prevents duplicates.
        builder.HasIndex(m => new { m.User1Id, m.User2Id }).IsUnique();
        builder.HasIndex(m => m.User2Id);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.User1Id).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.User2Id).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(m => m.Text).HasMaxLength(Message.TextMaxLength);
        builder.HasIndex(m => new { m.MatchId, m.SentAt });

        builder.HasOne<Match>().WithMany().HasForeignKey(m => m.MatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Cascade);
    }
}
