namespace SagaSales.BuildingBlocks.EventBus;

using SagaSales.Shared.Events;

/// <summary>
/// Publishes integration events across service boundaries.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}

/// <summary>
/// Base class for MassTransit consumers that handle an integration event.
/// Derive from this and register with <c>x.AddConsumer&lt;THandler&gt;()</c>.
/// </summary>
public abstract class IntegrationEventHandler<TEvent> : MassTransit.IConsumer<TEvent>
    where TEvent : class, IIntegrationEvent
{
    public Task Consume(MassTransit.ConsumeContext<TEvent> context)
        => Handle(context.Message, context.CancellationToken);

    public abstract Task Handle(TEvent @event, CancellationToken cancellationToken);
}
