using Reconnect.Domain.Common;

namespace Reconnect.Domain.Profiles;

/// <summary>Public-facing data of a user. Shares its primary key with the identity user.</summary>
public sealed class Profile
{
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 500;

    private Profile() { }

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public DateOnly BirthDate { get; private set; }
    public string? Bio { get; private set; }
    public bool IsVerified { get; private set; }

    public static Profile Create(Guid userId, string displayName, DateOnly birthDate, DateOnly today)
    {
        if (!AgePolicy.IsAdult(birthDate, today))
        {
            throw new DomainException($"Users must be at least {AgePolicy.MinimumAge} years old.");
        }

        var profile = new Profile { UserId = userId, BirthDate = birthDate };
        profile.Update(displayName, bio: null);
        return profile;
    }

    public void Update(string displayName, string? bio)
    {
        displayName = displayName.Trim();
        if (displayName.Length is 0 or > DisplayNameMaxLength)
        {
            throw new DomainException($"Display name must be 1-{DisplayNameMaxLength} characters.");
        }
        if (bio is { Length: > BioMaxLength })
        {
            throw new DomainException($"Bio must be at most {BioMaxLength} characters.");
        }

        DisplayName = displayName;
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
    }

    public void MarkVerified() => IsVerified = true;

    public int AgeOn(DateOnly today) => AgePolicy.AgeOn(BirthDate, today);
}
