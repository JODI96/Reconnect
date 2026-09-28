using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.Profiles.Domain;

/// <summary>Public-facing data of a user. Shares its id with the account in the Identity module.</summary>
internal sealed class Profile
{
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 500;
    public const int LookMaxLength = 4000;

    private Profile() { }

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public DateOnly BirthDate { get; private set; }
    public string? Bio { get; private set; }
    public bool IsVerified { get; private set; }

    /// <summary>The avatar's look from the character creator (JSON of AvatarLookDto, checked by the endpoint); null = none yet.</summary>
    public string? Look { get; private set; }

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
        displayName = (displayName ?? "").Trim();
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

    public void ChangeLook(string lookJson)
    {
        if (string.IsNullOrWhiteSpace(lookJson) || lookJson.Length > LookMaxLength)
        {
            throw new DomainException($"A look must be 1-{LookMaxLength} characters.");
        }
        Look = lookJson;
    }

    public int AgeOn(DateOnly today) => AgePolicy.AgeOn(BirthDate, today);
}
