using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Social.Domain;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Social.Infrastructure;

internal sealed class SocialDbContext(DbContextOptions<SocialDbContext> options) : DbContext(options)
{
    public const string Schema = "social";

    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Like>(like =>
        {
            like.HasIndex(l => new { l.FromUserId, l.ToUserId }).IsUnique();
            like.HasIndex(l => l.ToUserId);
        });

        builder.Entity<Match>(match =>
        {
            // Pair is stored ordered (User1Id < User2Id), so this index prevents duplicates.
            match.HasIndex(m => new { m.User1Id, m.User2Id }).IsUnique();
            match.HasIndex(m => m.User2Id);
        });

        builder.Entity<Message>(message =>
        {
            message.Property(m => m.Text).HasMaxLength(Message.TextMaxLength);
            message.HasIndex(m => new { m.MatchId, m.SentAt });
            message.HasOne<Match>().WithMany().HasForeignKey(m => m.MatchId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class SocialDesignTimeFactory : ModuleDesignTimeFactory<SocialDbContext>
{
    protected override string Schema => SocialDbContext.Schema;
}
