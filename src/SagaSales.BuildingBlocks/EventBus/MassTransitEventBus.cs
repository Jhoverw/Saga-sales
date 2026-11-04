namespace SagaSales.BuildingBlocks.EventBus;

using SagaSales.Shared.Events;

/// <summary>
/// MassTransit-backed implementation of <see cref="IEventBus"/>.
/// </summary>
public sealed class MassTransitEventBus(
    MassTransit.IPublishEndpoint publishEndpoint,
    ILogger<MassTransitEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        logger.LogInformation(
            "Publishing integration event {EventType} ({EventId})",
            typeof(TEvent).Name,
            @event.EventId);

        await publishEndpoint.Publish(@event, cancellationToken);
    }
}
