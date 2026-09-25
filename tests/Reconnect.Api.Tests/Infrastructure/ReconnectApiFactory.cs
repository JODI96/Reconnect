using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Reconnect.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real API against a throw-away PostGIS container (Docker required).
/// Migrations are applied on startup. Redis/Blob connection strings are placeholders –
/// they are not contacted by the tested endpoints.
/// </summary>
public sealed class ReconnectApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:reconnectdb", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", "localhost:6379,abortConnect=false");
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-only-signing-key-0123456789-abcdefghijklmnop");
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ReconnectApiFactory>
{
    public const string Name = "Api";
}
