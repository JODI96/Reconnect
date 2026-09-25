using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Reconnect.Domain.Profiles;
using Reconnect.Infrastructure.Identity;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Auth;

/// <summary>
/// DEVELOPMENT ONLY: creates a well-known admin account (default "Admin" / "Admin") for quick
/// logins while developing. Runs only if the environment is Development AND "DevAdmin:Enabled"
/// is true (set in appsettings.Development.json). The password deliberately bypasses the
/// password rules, so this account must never exist outside a local development database.
/// </summary>
public static partial class DevAdminSeeder
{
    public const string AdminRole = "Admin";

    public static async Task SeedDevAdminIfEnabledAsync(this WebApplication app)
    {
        var options = app.Configuration.GetSection("DevAdmin");
        if (!app.Environment.IsDevelopment() || !options.GetValue<bool>("Enabled"))
        {
            return;
        }

        var userName = options["UserName"] ?? "Admin";
        var password = options["Password"] ?? "Admin";

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<ReconnectDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

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
            db.Profiles.Add(Profile.Create(admin.Id, userName, new DateOnly(1990, 1, 1), DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)));
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

        LogDevAdminAvailable(app.Logger, userName);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DEV ONLY: admin account '{UserName}' is available (DevAdmin:Enabled).")]
    private static partial void LogDevAdminAvailable(ILogger logger, string userName);

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {action}: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
