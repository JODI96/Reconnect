using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Wallet.Domain;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Wallet.Infrastructure;

internal sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public const string Schema = "wallet";

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Account>(account =>
        {
            account.HasKey(a => a.UserId);
            account.ToTable("accounts", t => t.HasCheckConstraint("ck_accounts_balance_not_negative", "balance_rappen >= 0"));
        });

        builder.Entity<LedgerEntry>(entry =>
        {
            entry.ToTable("ledger");
            entry.Property(e => e.Kind).HasMaxLength(LedgerEntry.KindMaxLength);
            entry.Property(e => e.Description).HasMaxLength(LedgerEntry.DescriptionMaxLength);
            entry.Property(e => e.Reference).HasMaxLength(LedgerEntry.ReferenceMaxLength);
            entry.HasIndex(e => new { e.UserId, e.CreatedAt });
            entry.HasOne<Account>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class WalletDesignTimeFactory : ModuleDesignTimeFactory<WalletDbContext>
{
    protected override string Schema => WalletDbContext.Schema;
}
