using Microsoft.Extensions.DependencyInjection;

namespace Reconnect.SharedKernel.Events;

/// <summary>Something that happened in one module that other modules may react to (e.g. "user blocked").</summary>
public interface IIntegrationEvent;

/// <summary>Reacts to an integration event of another module. Registered with <see cref="EventBusExtensions.AddEventHandler{TEvent,THandler}"/>.</summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default) where TEvent : IIntegrationEvent;
}

/// <summary>
/// Delivers events synchronously, in-process, within the current request scope – simple and consistent.
/// If a module is split into its own service later, this is the seam to replace with a message broker
/// (plus an outbox for reliability) without touching publishers or handlers.
/// </summary>
internal sealed class InProcessEventBus(IServiceProvider services) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, ct);
        }
    }
}

public static class EventBusExtensions
{
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent> =>
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
}
