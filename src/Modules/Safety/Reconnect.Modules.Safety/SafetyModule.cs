using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reconnect.Modules.Safety.Features;
using Reconnect.Modules.Safety.Infrastructure;
using Reconnect.Modules.Safety.Public;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Safety;

/// <summary>
/// Trust &amp; safety: blocks (both directions, respected by every module through <see cref="IBlockQueries"/>)
/// and reports for moderation. Publishes <see cref="UserBlocked"/> so other modules can clean up.
/// </summary>
public sealed class SafetyModule : IModule
{
    public string Name => "safety";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<SafetyDbContext>(Name);
        builder.Services.AddScoped<IBlockQueries, BlockQueries>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        BlockEndpoints.Map(api);
        ReportEndpoints.Map(api);
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<SafetyDbContext>(ct);
}
