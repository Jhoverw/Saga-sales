namespace SagaSales.Catalog.Commands;

/// <summary>
/// Sets the price of one product for one customer type (D5). The resolution rule itself lives in
/// <c>sp_price_resolve</c>: a missing entry means the base price applies.
/// </summary>
public record SetProductPriceCommand(Guid ProductId, CustomerType CustomerType, decimal Price)
    : ICommand<PriceQuoteDto>;

public sealed class SetProductPriceCommandHandler(IPriceListRepository repository)
    : ICommandHandler<SetProductPriceCommand, PriceQuoteDto>
{
    public async Task<Result<PriceQuoteDto>> Handle(
        SetProductPriceCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));
        Guard.AgainstNegativeOrZero(request.Price, nameof(request.Price));

        var quote = await repository.SetAsync(
            request.ProductId, request.CustomerType, request.Price, cancellationToken);

        return Result<PriceQuoteDto>.Success(quote);
    }
}
