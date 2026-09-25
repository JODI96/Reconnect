using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reconnect.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef migrations add</c>. No database connection is needed for that,
/// so the connection string is a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReconnectDbContext>
{
    public ReconnectDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReconnectDbContext>();
        options.ConfigureReconnect("Host=localhost;Database=reconnect_design");
        return new ReconnectDbContext(options.Options);
    }
}
