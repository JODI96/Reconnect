using Microsoft.Extensions.DependencyInjection;
using Reconnect.SharedKernel.Events;

namespace Reconnect.UnitTests;

public sealed class EventBusTests
{
    private sealed record Pinged(int Value) : IIntegrationEvent;

    private sealed class Recorder
    {
        public List<string> Calls { get; } = [];
    }

    private sealed class FirstHandler(Recorder recorder) : IIntegrationEventHandler<Pinged>
    {
        public Task HandleAsync(Pinged integrationEvent, CancellationToken ct)
        {
            recorder.Calls.Add($"first:{integrationEvent.Value}");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondHandler(Recorder recorder) : IIntegrationEventHandler<Pinged>
    {
        public Task HandleAsync(Pinged integrationEvent, CancellationToken ct)
        {
            recorder.Calls.Add($"second:{integrationEvent.Value}");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IIntegrationEventHandler<Pinged>
    {
        public Task HandleAsync(Pinged integrationEvent, CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task All_handlers_run_in_registration_order()
    {
        var services = new ServiceCollection()
            .AddSingleton<Recorder>()
            .AddEventHandler<Pinged, FirstHandler>()
            .AddEventHandler<Pinged, SecondHandler>()
            .AddScoped<IEventBus, InProcessEventBus>()
            .BuildServiceProvider();

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(new Pinged(7));

        Assert.Equal(["first:7", "second:7"], services.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public async Task Publishing_without_handlers_does_nothing()
    {
        var services = new ServiceCollection().AddScoped<IEventBus, InProcessEventBus>().BuildServiceProvider();

        await services.CreateScope().ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(new Pinged(1));
    }

    [Fact]
    public async Task Handler_exceptions_reach_the_publisher_so_it_can_compensate()
    {
        var services = new ServiceCollection()
            .AddEventHandler<Pinged, FailingHandler>()
            .AddScoped<IEventBus, InProcessEventBus>()
            .BuildServiceProvider();

        var bus = services.CreateScope().ServiceProvider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync(new Pinged(1)));
    }
}
