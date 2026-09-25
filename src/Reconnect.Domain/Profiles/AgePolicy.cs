namespace Reconnect.Domain.Profiles;

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
