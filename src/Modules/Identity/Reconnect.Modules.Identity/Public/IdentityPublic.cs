using Reconnect.SharedKernel.Events;

namespace Reconnect.Modules.Identity.Public;

/// <summary>What other modules may ask about accounts.</summary>
public interface IUserDirectory
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken ct);

    Task<Guid?> FindIdByUserNameAsync(string userName, CancellationToken ct);
}

/// <summary>
/// A new account was created. The Profiles module creates the profile; if that throws a
/// DomainException (e.g. invalid display name) the registration is rolled back.
/// </summary>
public sealed record UserRegistered(Guid UserId, string DisplayName, DateOnly BirthDate) : IIntegrationEvent;
