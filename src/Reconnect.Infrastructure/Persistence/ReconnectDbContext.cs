using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Reconnect.Domain.Buildings;
using Reconnect.Domain.Profiles;
using Reconnect.Domain.Rooms;
using Reconnect.Domain.Safety;
using Reconnect.Domain.Social;
using Reconnect.Infrastructure.Identity;

namespace Reconnect.Infrastructure.Persistence;

public sealed class ReconnectDbContext(DbContextOptions<ReconnectDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");
        builder.ApplyConfigurationsFromAssembly(typeof(ReconnectDbContext).Assembly);
    }
}
