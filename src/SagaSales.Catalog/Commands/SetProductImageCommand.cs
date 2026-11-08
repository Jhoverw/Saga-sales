namespace SagaSales.Catalog.Commands;

/// <summary>
/// Sets or clears the product's main picture. The file has already been written to the image
/// volume by the controller; this command only records the URL path (E-13 · US-026).
/// </summary>
public record SetProductImageCommand(Guid ProductId, string? ImagePath) : ICommand<ProductDto>;

public sealed class SetProductImageCommandHandler(IProductRepository repository)
    : ICommandHandler<SetProductImageCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(SetProductImageCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));

        var product = await repository.SetImageAsync(request.ProductId, request.ImagePath, cancellationToken);
        return Result<ProductDto>.Success(product.ToDto());
    }
}
