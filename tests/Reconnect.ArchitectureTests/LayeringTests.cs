using NetArchTest.Rules;
using Reconnect.Contracts;
using Reconnect.SharedKernel.Modules;

namespace Reconnect.ArchitectureTests;

/// <summary>Rules for the projects around the modules.</summary>
public sealed class LayeringTests
{
    [Fact]
    public void SharedKernel_does_not_know_any_module_or_the_host()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .ShouldNot().HaveDependencyOnAny("Reconnect.Modules", "Reconnect.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_have_no_package_dependencies_so_Unity_can_use_them()
    {
        var references = typeof(ApiRoutes).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.All(references, name => Assert.Equal("netstandard", name));
    }

    [Fact]
    public void Host_contains_no_business_logic()
    {
        // The API host only composes modules: no DbContexts, hubs or endpoints of its own.
        var hostTypes = typeof(Api.ResourceNames).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Reconnect.Api", StringComparison.Ordinal) == true)
            .Where(t => typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(t)
                        || typeof(Microsoft.AspNetCore.SignalR.Hub).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(hostTypes);
    }
}
