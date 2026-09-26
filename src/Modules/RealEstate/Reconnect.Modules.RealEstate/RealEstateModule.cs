using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Reconnect.Modules.RealEstate.Features;
using Reconnect.Modules.RealEstate.Infrastructure;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.RealEstate;

/// <summary>
/// Property that users can buy and sell with in-game Swiss francs – for now private offices in the Prime Tower,
/// later in every building. Pays via the Wallet module, creates/removes the owner's room via the Rooms module.
/// </summary>
public sealed class RealEstateModule : IModule
{
    public string Name => "realestate";

    public void Register(IHostApplicationBuilder builder) => builder.AddModuleDbContext<RealEstateDbContext>(Name);

    public void MapEndpoints(IEndpointRouteBuilder api) => OfficeEndpoints.Map(api);

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<RealEstateDbContext>(ct);

    public Task SeedAsync(IServiceProvider services, CancellationToken ct) => OfficeSeeder.SeedAsync(services, ct);
}
