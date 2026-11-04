namespace SagaSales.Catalog.Queries;

/// <summary>
/// Reads a single product. The previous controller fetched 10 000 products and filtered in
/// memory to emulate this query.
/// </summary>
public record GetProductQuery(Guid Id) : IQuery<ProductDto>;

public sealed class GetProductQueryHandler(
    CatalogDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetProductQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", request.Id);
        }

        return Result<ProductDto>.Success(product.ToDto());
    }
}
