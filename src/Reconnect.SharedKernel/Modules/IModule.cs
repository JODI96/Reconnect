using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Reconnect.SharedKernel.Modules;

/// <summary>
/// A self-contained part of the backend (Identity, Rooms …): its own domain, database schema,
/// migrations, endpoints and services. The API host only lists the modules; everything else lives here.
/// Modules talk to each other only through public interfaces (namespace <c>*.Public</c>) and integration events.
/// </summary>
public interface IModule
{
    /// <summary>Short lower-case name; also the database schema of the module.</summary>
    string Name { get; }

    /// <summary>Registers the module's services, DbContext, event handlers …</summary>
    void Register(IHostApplicationBuilder builder);

    /// <summary>Maps HTTP endpoints and hubs into the versioned API group (e.g. <c>/v1</c>).</summary>
    void MapEndpoints(IEndpointRouteBuilder api);

    /// <summary>Applies the module's database migrations. Runs for all modules before any seeding.</summary>
    Task MigrateAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Seeds data (reference data, development samples). Runs after all modules are migrated.</summary>
    Task SeedAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}
