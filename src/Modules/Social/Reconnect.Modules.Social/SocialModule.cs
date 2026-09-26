using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Reconnect.Contracts.Hubs;
using Reconnect.Modules.Safety.Public;
using Reconnect.Modules.Social.Features;
using Reconnect.Modules.Social.Hubs;
using Reconnect.Modules.Social.Infrastructure;
using Reconnect.SharedKernel.Events;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Social;

/// <summary>Likes, matches and the match chat (SignalR <c>/hubs/chat</c>).</summary>
public sealed class SocialModule : IModule
{
    public string Name => "social";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<SocialDbContext>(Name);
        builder.Services.AddEventHandler<UserBlocked, RemoveConnectionOnBlock>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        LikeEndpoints.Map(api);
        api.MapHub<ChatHub>(ChatHubContract.RelativePath);
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<SocialDbContext>(ct);
}
