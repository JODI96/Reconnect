using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers <see cref="ReconnectDbContext"/> for PostgreSQL + PostGIS.</summary>
    public static IServiceCollection AddReconnectPersistence(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditableInterceptor>();

        services.AddDbContext<ReconnectDbContext>((sp, options) => options.ConfigureReconnect(connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditableInterceptor>()));

        return services;
    }

    internal static DbContextOptionsBuilder ConfigureReconnect(this DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            })
            .UseSnakeCaseNamingConvention();
}
