namespace SagaSales.Shared.Events;

/// <summary>
/// An event that crosses a service boundary, typically published on the message bus.
/// </summary>
public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }

    DateTime OccurredOn { get; }
}

/// <summary>
/// Base record for integration events: gives every event a stable identity and timestamp.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
