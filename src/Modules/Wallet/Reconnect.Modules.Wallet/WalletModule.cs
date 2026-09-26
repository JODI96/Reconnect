using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reconnect.Modules.Wallet.Domain;
using Reconnect.Modules.Wallet.Features;
using Reconnect.Modules.Wallet.Infrastructure;
using Reconnect.Modules.Wallet.Public;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Persistence;

namespace Reconnect.Modules.Wallet;

/// <summary>
/// In-game money in Swiss francs: one account per user (starting capital on first use) and an append-only
/// ledger. Other modules pay and get paid through <see cref="IWallet"/>. The economy system builds on this.
/// </summary>
public sealed class WalletModule : IModule
{
    public string Name => "wallet";

    public void Register(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<WalletDbContext>(Name);
        builder.Services.AddOptions<WalletOptions>().Bind(builder.Configuration.GetSection(WalletOptions.SectionName));
        builder.Services.AddScoped<IWallet, WalletService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => WalletEndpoints.Map(api);

    public Task MigrateAsync(IServiceProvider services, CancellationToken ct) => services.MigrateAsync<WalletDbContext>(ct);
}
