namespace SagaSales.Shared.Events;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
    Guid EventId { get; }
    string EventType { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; }

    protected DomainEvent()
    {
        EventType = GetType().Name;
    }
}