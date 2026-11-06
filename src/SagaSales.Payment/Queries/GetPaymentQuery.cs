namespace SagaSales.Payment.Queries;

using SagaSales.Payment.Data;

public record GetPaymentQuery(Guid Id) : IQuery<PaymentDto>;

public sealed class GetPaymentQueryHandler(IPaymentRepository repository)
    : IQueryHandler<GetPaymentQuery, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
    {
        var payment = await repository.GetAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Payment", request.Id);

        return Result<PaymentDto>.Success(payment);
    }
}

/// <summary>Every payment of one order: what was charged, what was refunded and when.</summary>
public record GetPaymentsByOrderQuery(Guid OrderId) : IQuery<IReadOnlyList<PaymentDto>>;

public sealed class GetPaymentsByOrderQueryHandler(IPaymentRepository repository)
    : IQueryHandler<GetPaymentsByOrderQuery, IReadOnlyList<PaymentDto>>
{
    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(
        GetPaymentsByOrderQuery request,
        CancellationToken cancellationToken)
    {
        var payments = await repository.ListByOrderAsync(request.OrderId, cancellationToken);
        return Result<IReadOnlyList<PaymentDto>>.Success(payments);
    }
}
