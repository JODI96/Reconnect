using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Profiles.Domain;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Profiles.Infrastructure;

internal sealed class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : DbContext(options)
{
    public const string Schema = "profiles";

    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.Entity<Profile>(profile =>
        {
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.DisplayName).HasMaxLength(Profile.DisplayNameMaxLength);
            profile.Property(p => p.Bio).HasMaxLength(Profile.BioMaxLength);
            profile.Property(p => p.Look).HasMaxLength(Profile.LookMaxLength);
        });
    }
}

internal sealed class ProfilesDesignTimeFactory : ModuleDesignTimeFactory<ProfilesDbContext>
{
    protected override string Schema => ProfilesDbContext.Schema;
}
