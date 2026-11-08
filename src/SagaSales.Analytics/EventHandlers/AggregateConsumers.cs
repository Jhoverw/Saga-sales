namespace SagaSales.Analytics.EventHandlers;

/// <summary>A payment authorized for an order counts as one more successful payment that day.</summary>
public sealed class PaymentAuthorizedConsumer(IAnalyticsRepository repository)
    : IntegrationEventHandler<PaymentAuthorizedEvent>
{
    public override async Task Handle(PaymentAuthorizedEvent @event, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(@event.AuthorizedAt);
        await repository.UpsertPaymentAsync(@event.TenantId, date, cancellationToken);
    }
}

/// <summary>A new customer lands as one entry in the activity feed.</summary>
public sealed class CustomerCreatedConsumer(IAnalyticsRepository repository)
    : IntegrationEventHandler<CustomerCreatedEvent>
{
    public override async Task Handle(CustomerCreatedEvent @event, CancellationToken cancellationToken)
    {
        await repository.AddActivityAsync(
            @event.TenantId,
            @event.CustomerId,
            "customer.created",
            @event.OccurredOn,
            cancellationToken);
    }
}
