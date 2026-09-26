using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reconnect.SharedKernel.Events;

namespace Reconnect.SharedKernel.Modules;

public static class ModuleExtensions
{
    /// <summary>Registers the modules (in dependency order) and the shared building blocks.</summary>
    public static IHostApplicationBuilder AddModules(this IHostApplicationBuilder builder, params IModule[] modules)
    {
        builder.Services.AddSingleton(new ModuleRegistry(modules));
        builder.Services.AddScoped<IEventBus, InProcessEventBus>();
        foreach (var module in modules)
        {
            module.Register(builder);
        }
        return builder;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder api)
    {
        foreach (var module in api.ServiceProvider.GetRequiredService<ModuleRegistry>().Modules)
        {
            module.MapEndpoints(api);
        }
        return api;
    }

    /// <summary>
    /// Migrates all modules (if "Database:MigrateOnStartup" is true), then seeds all modules.
    /// Two phases, because seeds may use other modules (e.g. the dev admin needs the profile table).
    /// </summary>
    public static async Task InitializeModulesAsync(this WebApplication app, CancellationToken ct = default)
    {
        var modules = app.Services.GetRequiredService<ModuleRegistry>().Modules;
        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            foreach (var module in modules)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await module.MigrateAsync(scope.ServiceProvider, ct);
            }
        }
        foreach (var module in modules)
        {
            await using var scope = app.Services.CreateAsyncScope();
            await module.SeedAsync(scope.ServiceProvider, ct);
        }
    }
}

public sealed class ModuleRegistry(IReadOnlyList<IModule> modules)
{
    public IReadOnlyList<IModule> Modules { get; } = modules;
}
