using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reconnect.Contracts.Hubs;
using Reconnect.Modules.Rooms.Features;
using Reconnect.Modules.Rooms.Hubs;
using Reconnect.Modules.Rooms.Infrastructure;
using Reconnect.Modules.Rooms.Infrastructure.Seeding;
using Reconnect.Modules.Rooms.Public;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Rooms;

/// <summary>
/// User rooms inside buildings: layout (jsonb), live presence and minigames via SignalR
/// (<c>/hubs/room</c>, state in Redis), tower floors with capacities, lift and lift queue.
/// Needs an <c>IConnectionMultiplexer</c> from the host.
/// </summary>
public sealed class RoomsModule : IModule
{
    public string Name => "rooms";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<RoomsDbContext>(Name);
        builder.Services.AddScoped<RoomReader>();
        builder.Services.AddSingleton<IRoomPresenceStore, RedisRoomPresenceStore>();
        builder.Services.AddSingleton<IRoomGameStore, RedisRoomGameStore>();
        builder.Services.AddSingleton<IElevatorQueue, RedisElevatorQueue>();
        builder.Services.AddScoped<IRoomProvisioning, RoomProvisioning>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        RoomEndpoints.Map(api);
        TowerEndpoints.Map(api);
        api.MapHub<RoomHub>(RoomHubContract.RelativePath);
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<RoomsDbContext>(ct);

    public async Task SeedAsync(IServiceProvider services, CancellationToken ct)
    {
        await PrimeTowerFloors.SeedAsync(services, ct);   // part of the game, all environments
        await ShowcaseRooms.SeedIfEnabledAsync(services, ct);
    }
}
