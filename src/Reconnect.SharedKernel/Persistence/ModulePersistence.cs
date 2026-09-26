using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Reconnect.SharedKernel.Persistence;

/// <summary>
/// Every module has its own DbContext in its own PostgreSQL schema (identity, profiles, rooms …) with its
/// own migrations. Modules never share tables or foreign keys – that keeps them independent and lets a
/// module move to its own database later.
/// </summary>
public static class ModulePersistence
{
    /// <summary>Aspire connection name of the (single, shared) PostgreSQL database.</summary>
    public const string ConnectionName = "reconnectdb";

    public static IHostApplicationBuilder AddModuleDbContext<TContext>(this IHostApplicationBuilder builder, string schema,
        bool spatial = false) where TContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is missing. Start the app via Reconnect.AppHost.");

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped<AuditableInterceptor>();
        builder.Services.AddDbContext<TContext>((sp, options) => options
            .ConfigureModule(connectionString, schema, spatial)
            .AddInterceptors(sp.GetRequiredService<AuditableInterceptor>()));
        builder.EnrichNpgsqlDbContext<TContext>();   // health check, tracing, retries
        return builder;
    }

    public static DbContextOptionsBuilder ConfigureModule(this DbContextOptionsBuilder options, string connectionString,
        string schema, bool spatial) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema);
                if (spatial)
                {
                    npgsql.UseNetTopologySuite();
                }
            })
            .UseSnakeCaseNamingConvention();

    public static async Task MigrateAsync<TContext>(this IServiceProvider services, CancellationToken ct) where TContext : DbContext =>
        await services.GetRequiredService<TContext>().Database.MigrateAsync(ct);
}

/// <summary>
/// Base for the per-module design-time factory used by <c>dotnet ef migrations add … --context XyzDbContext</c>.
/// No database connection is needed for that; the connection string is a placeholder.
/// </summary>
public abstract class ModuleDesignTimeFactory<TContext> : IDesignTimeDbContextFactory<TContext> where TContext : DbContext
{
    protected abstract string Schema { get; }
    protected virtual bool Spatial => false;

    public TContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TContext>();
        options.ConfigureModule("Host=localhost;Database=reconnect_design", Schema, Spatial);
        return (TContext)Activator.CreateInstance(typeof(TContext), options.Options)!;
    }
}
