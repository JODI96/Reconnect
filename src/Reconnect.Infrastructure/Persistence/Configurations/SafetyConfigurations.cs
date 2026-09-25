using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconnect.Domain.Rooms;
using Reconnect.Domain.Safety;
using Reconnect.Domain.Social;
using Reconnect.Infrastructure.Identity;

namespace Reconnect.Infrastructure.Persistence.Configurations;

internal sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.HasIndex(b => new { b.BlockerId, b.BlockedId }).IsUnique();
        builder.HasIndex(b => b.BlockedId);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(b => b.BlockerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(b => b.BlockedId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Comment).HasMaxLength(Report.CommentMaxLength);
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => r.ReportedUserId);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.ReportedUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Room>().WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Message>().WithMany().HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.SetNull);
    }
}
