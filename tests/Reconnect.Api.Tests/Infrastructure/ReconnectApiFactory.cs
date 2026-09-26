using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Reconnect.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real API against throw-away PostGIS and Redis containers (Docker required).
/// Migrations are applied on startup. The blob connection string is a placeholder –
/// blob storage is not used by the tested endpoints yet.
/// </summary>
public sealed class ReconnectApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public async Task InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:reconnectdb", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", _redis.GetConnectionString());
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-only-signing-key-0123456789-abcdefghijklmnop");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        // Many test users share one client IP – the limits themselves are tested separately.
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:WritesPerMinute", "100000");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ReconnectApiFactory>
{
    public const string Name = "Api";
}
