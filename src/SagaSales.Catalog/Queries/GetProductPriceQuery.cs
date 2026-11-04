namespace SagaSales.Catalog.Queries;

/// <summary>
/// The price that applies to one product for one customer type, including whether the list price
/// or the fallback base price was used (FR-007b).
/// </summary>
public record GetProductPriceQuery(Guid ProductId, CustomerType CustomerType) : IQuery<PriceQuoteDto>;

public sealed class GetProductPriceQueryHandler(IPriceListRepository repository)
    : IQueryHandler<GetProductPriceQuery, PriceQuoteDto>
{
    public async Task<Result<PriceQuoteDto>> Handle(
        GetProductPriceQuery request,
        CancellationToken cancellationToken)
    {
        var quote = await repository.ResolveAsync(request.ProductId, request.CustomerType, cancellationToken);
        return Result<PriceQuoteDto>.Success(quote);
    }
}
