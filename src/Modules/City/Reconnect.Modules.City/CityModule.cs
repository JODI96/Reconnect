using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reconnect.Modules.City.Features;
using Reconnect.Modules.City.Infrastructure;
using Reconnect.Modules.City.Public;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.City;

/// <summary>
/// The real city: buildings (PostGIS) that act as entrances to rooms. Later home of the
/// swisstopo import pipeline and districts.
/// </summary>
public sealed class CityModule : IModule
{
    public string Name => "city";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<CityDbContext>(Name, spatial: true);
        builder.Services.AddScoped<ICityDirectory, CityDirectory>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => BuildingEndpoints.Map(api);

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<CityDbContext>(ct);
}
