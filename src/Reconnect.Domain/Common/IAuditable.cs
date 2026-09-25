namespace Reconnect.Domain.Common;

/// <summary>Entities with creation/update timestamps; set automatically on SaveChanges.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
