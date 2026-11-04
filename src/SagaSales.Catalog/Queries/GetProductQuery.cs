namespace SagaSales.Catalog.Queries;

public record GetProductQuery(Guid Id) : IQuery<ProductDto>;

public sealed class GetProductQueryHandler(IProductRepository repository)
    : IQueryHandler<GetProductQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await repository.GetAsync(request.Id, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", request.Id);
        }

        return Result<ProductDto>.Success(product.ToDto());
    }
}
