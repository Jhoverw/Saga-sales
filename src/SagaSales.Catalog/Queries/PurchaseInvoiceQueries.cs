namespace SagaSales.Catalog.Queries;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

/// <summary>Busca productos por código exacto o por parte del nombre para el detalle de la compra.</summary>
public record FindPurchaseProductsQuery(string Term, int MaxRows = 15) : IQuery<IReadOnlyList<PurchaseProductDto>>;

public sealed class FindPurchaseProductsQueryHandler(IPurchaseInvoiceRepository repository)
    : IQueryHandler<FindPurchaseProductsQuery, IReadOnlyList<PurchaseProductDto>>
{
    public async Task<Result<IReadOnlyList<PurchaseProductDto>>> Handle(
        FindPurchaseProductsQuery request,
        CancellationToken cancellationToken)
    {
        var term = (request.Term ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return Result<IReadOnlyList<PurchaseProductDto>>.Success([]);
        }

        var rows = await repository.FindProductsAsync(term, Math.Clamp(request.MaxRows, 1, 50), cancellationToken);
        return Result<IReadOnlyList<PurchaseProductDto>>.Success(rows);
    }
}

public record GetPayablesQuery(bool IncludePaid = false) : IQuery<IReadOnlyList<PayableDto>>;

public sealed class GetPayablesQueryHandler(IPurchaseInvoiceRepository repository)
    : IQueryHandler<GetPayablesQuery, IReadOnlyList<PayableDto>>
{
    public async Task<Result<IReadOnlyList<PayableDto>>> Handle(GetPayablesQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<PayableDto>>.Success(await repository.ListPayablesAsync(request.IncludePaid, cancellationToken));
}

public record GetPayablePaymentsQuery(Guid PayableId) : IQuery<IReadOnlyList<PayablePaymentDto>>;

public sealed class GetPayablePaymentsQueryHandler(IPurchaseInvoiceRepository repository)
    : IQueryHandler<GetPayablePaymentsQuery, IReadOnlyList<PayablePaymentDto>>
{
    public async Task<Result<IReadOnlyList<PayablePaymentDto>>> Handle(
        GetPayablePaymentsQuery request,
        CancellationToken cancellationToken)
        => Result<IReadOnlyList<PayablePaymentDto>>.Success(
            await repository.ListPaymentsAsync(request.PayableId, cancellationToken));
}
