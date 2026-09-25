using Microsoft.AspNetCore.Identity;

namespace Reconnect.Infrastructure.Identity;

/// <summary>
/// Login account (email + password). Public data lives in <see cref="Domain.Profiles.Profile"/>
/// with the same primary key, so the domain never depends on ASP.NET Identity.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
}
