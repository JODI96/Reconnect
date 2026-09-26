using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Identity.Infrastructure;
using Reconnect.Modules.Identity.Public;

namespace Reconnect.Modules.Identity.Features;

internal sealed class UserDirectory(IdentityDbContext db) : IUserDirectory
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => db.Users.AnyAsync(u => u.Id == userId, ct);

    public async Task<Guid?> FindIdByUserNameAsync(string userName, CancellationToken ct) =>
        await db.Users.Where(u => u.UserName == userName).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
}
