using System.Reflection;

namespace Reconnect.Api.Common.Endpoints;

/// <summary>
/// A feature's endpoints. Every non-abstract implementation in this assembly is discovered
/// and mapped automatically by <see cref="EndpointModuleExtensions.MapEndpointModules"/>.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder app);
}

public static class EndpointModuleExtensions
{
    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder app)
    {
        var modules = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpointModule)))
            .Select(t => (IEndpointModule)Activator.CreateInstance(t)!);

        foreach (var module in modules)
        {
            module.MapEndpoints(app);
        }
        return app;
    }
}
