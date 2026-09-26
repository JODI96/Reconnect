using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Safety.Domain;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Safety.Infrastructure;

internal sealed class SafetyDbContext(DbContextOptions<SafetyDbContext> options) : DbContext(options)
{
    public const string Schema = "safety";

    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Block>(block =>
        {
            block.HasIndex(b => new { b.BlockerId, b.BlockedId }).IsUnique();
            block.HasIndex(b => b.BlockedId);
        });

        builder.Entity<Report>(report =>
        {
            report.Property(r => r.Reason).HasConversion<string>().HasMaxLength(32);
            report.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
            report.Property(r => r.Comment).HasMaxLength(Report.CommentMaxLength);
            report.HasIndex(r => new { r.Status, r.CreatedAt });
            report.HasIndex(r => r.ReportedUserId);
        });
    }
}

internal sealed class SafetyDesignTimeFactory : ModuleDesignTimeFactory<SafetyDbContext>
{
    protected override string Schema => SafetyDbContext.Schema;
}
