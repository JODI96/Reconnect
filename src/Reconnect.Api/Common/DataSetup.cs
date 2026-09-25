using Microsoft.EntityFrameworkCore;
using Reconnect.Infrastructure;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Common;

/// <summary>Wires the backing services. Connection strings are provided by Aspire (AppHost).</summary>
public static class DataSetup
{
    public const string DatabaseName = "reconnectdb";
    public const string RedisName = "redis";
    public const string BlobsName = "blobs";

    public static IHostApplicationBuilder AddReconnectData(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(DatabaseName)
            ?? throw new InvalidOperationException($"Connection string '{DatabaseName}' is missing. Start the app via Reconnect.AppHost.");

        builder.Services.AddReconnectPersistence(connectionString);
        builder.EnrichNpgsqlDbContext<ReconnectDbContext>();   // health check, tracing, retries

        builder.AddRedisClient(RedisName);                     // presence/cache (used from phase 3)
        builder.AddAzureBlobServiceClient(BlobsName);          // avatars / room images (later)

        return builder;
    }

    /// <summary>Applies pending migrations when "Database:MigrateOnStartup" is true (Development).</summary>
    public static async Task MigrateDatabaseIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReconnectDbContext>();
        await db.Database.MigrateAsync();
    }
}
