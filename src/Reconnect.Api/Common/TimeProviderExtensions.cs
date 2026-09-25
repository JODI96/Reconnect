namespace Reconnect.Api.Common;

public static class TimeProviderExtensions
{
    public static DateOnly GetUtcToday(this TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
