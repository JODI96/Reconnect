namespace Reconnect.SharedKernel.Domain;

/// <summary>Violation of a business rule. The API maps it to a 400 ProblemDetails response.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Entities with creation/update timestamps; set automatically on SaveChanges.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Platform-wide age rule: Reconnect is 18+. Used by registration and profiles.</summary>
public static class AgePolicy
{
    public const int MinimumAge = 18;

    public static int AgeOn(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }
        return age;
    }

    public static bool IsAdult(DateOnly birthDate, DateOnly today) => AgeOn(birthDate, today) >= MinimumAge;
}
