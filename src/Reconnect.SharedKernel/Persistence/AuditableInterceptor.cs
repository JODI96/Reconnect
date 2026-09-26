using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reconnect.SharedKernel.Domain;

namespace Reconnect.SharedKernel.Persistence;

/// <summary>Sets <see cref="IAuditable"/> timestamps on every SaveChanges.</summary>
public sealed class AuditableInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified || HasChangedOwnedEntities(entry))
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    // Owned JSON collections (e.g. a room layout) change without marking the owner as Modified.
    private static bool HasChangedOwnedEntities(EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry is not null &&
            r.TargetEntry.Metadata.IsOwned() &&
            r.TargetEntry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        || entry.Collections.Any(c =>
            c.CurrentValue is not null &&
            c.Metadata.TargetEntityType.IsOwned() &&
            c.CurrentValue.Cast<object>().Any(item =>
                entry.Context.Entry(item).State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
}
