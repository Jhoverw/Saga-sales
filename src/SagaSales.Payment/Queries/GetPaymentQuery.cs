namespace SagaSales.Payment.Queries;

public record GetPaymentQuery(Guid Id) : IQuery<PaymentDto>;

public sealed class GetPaymentQueryHandler(
    PaymentDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetPaymentQuery, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var payment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.TenantId == tenantId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Payment", request.Id);
        }

        return Result<PaymentDto>.Success(payment.ToDto());
    }
}

public record GetPaymentsByOrderQuery(Guid OrderId) : IQuery<IReadOnlyList<PaymentDto>>;

public sealed class GetPaymentsByOrderQueryHandler(
    PaymentDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetPaymentsByOrderQuery, IReadOnlyList<PaymentDto>>
{
    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(
        GetPaymentsByOrderQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == request.OrderId && p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<PaymentDto> result = payments.Select(p => p.ToDto()).ToList();
        return Result<IReadOnlyList<PaymentDto>>.Success(result);
    }
}
