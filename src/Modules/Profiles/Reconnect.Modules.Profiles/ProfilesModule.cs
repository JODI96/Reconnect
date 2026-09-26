using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reconnect.Modules.Identity.Public;
using Reconnect.Modules.Profiles.Features;
using Reconnect.Modules.Profiles.Infrastructure;
using Reconnect.Modules.Profiles.Public;
using Reconnect.SharedKernel.Events;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Profiles;

/// <summary>
/// Public-facing user data (display name, age, bio, verification). Creates the profile when a
/// user registers (<see cref="UserRegistered"/>); other modules read names via <see cref="IProfileDirectory"/>.
/// </summary>
public sealed class ProfilesModule : IModule
{
    public string Name => "profiles";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<ProfilesDbContext>(Name);
        builder.Services.AddScoped<IProfileDirectory, ProfileDirectory>();
        builder.Services.AddEventHandler<UserRegistered, CreateProfileOnRegistration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => ProfileEndpoints.Map(api);

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<ProfilesDbContext>(ct);
}
