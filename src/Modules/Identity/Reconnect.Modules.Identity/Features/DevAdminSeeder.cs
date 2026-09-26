using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reconnect.Modules.Identity.Infrastructure;
using Reconnect.Modules.Identity.Public;
using Reconnect.SharedKernel.Events;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Identity.Features;

/// <summary>
/// DEVELOPMENT ONLY: creates a well-known admin account (default "Admin" / "Admin") for quick
/// logins while developing. Runs only if the environment is Development AND "DevAdmin:Enabled"
/// is true (appsettings.Development.json). The password deliberately bypasses the password rules,
/// so this account must never exist outside a local development database.
/// </summary>
internal static partial class DevAdminSeeder
{
    private const string AdminRole = AppRoles.Admin;

    public static async Task SeedIfEnabledAsync(IServiceProvider services, CancellationToken ct)
    {
        var environment = services.GetRequiredService<IHostEnvironment>();
        var options = services.GetRequiredService<IConfiguration>().GetSection("DevAdmin");
        if (!environment.IsDevelopment() || !options.GetValue<bool>("Enabled"))
        {
            return;
        }

        var userName = options["UserName"] ?? "Admin";
        var password = options["Password"] ?? "Admin";
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var time = services.GetRequiredService<TimeProvider>();

        if (!await roles.RoleExistsAsync(AdminRole))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(AdminRole) { Id = Guid.CreateVersion7() });
        }

        var admin = await users.FindByNameAsync(userName);
        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = userName,
                Email = $"{userName.ToLowerInvariant()}@reconnect.local",
                EmailConfirmed = true,
                CreatedAt = time.GetUtcNow(),
            };
            EnsureSucceeded(await users.CreateAsync(admin), "create dev admin");
        }

        // Set the password directly (bypasses password validators) – also resets it if it was changed.
        admin.PasswordHash = users.PasswordHasher.HashPassword(admin, password);
        admin.LockoutEnd = null;
        admin.AccessFailedCount = 0;
        EnsureSucceeded(await users.UpdateAsync(admin), "set dev admin password");
        if (!await users.IsInRoleAsync(admin, AdminRole))
        {
            EnsureSucceeded(await users.AddToRoleAsync(admin, AdminRole), "add dev admin role");
        }

        // Profiles are idempotent: publishing again just makes sure the admin has a profile.
        await services.GetRequiredService<IEventBus>().PublishAsync(new UserRegistered(admin.Id, userName, new DateOnly(1990, 1, 1)), ct);

        LogDevAdminAvailable(services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevAdminSeeder)), userName);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DEV ONLY: admin account '{UserName}' is available (DevAdmin:Enabled).")]
    private static partial void LogDevAdminAvailable(ILogger logger, string userName);

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {action}: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
