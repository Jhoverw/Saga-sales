namespace SagaSales.Catalog.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;

/// <summary>
/// Price list per customer type, through <c>db/catalog/V003__functions_pricing.sql</c>.
/// All the fallback logic lives in <c>sp_price_resolve</c>; this repository only carries the answer.
/// </summary>
public interface IPriceListRepository
{
    Task<PriceQuoteDto> SetAsync(Guid productId, CustomerType customerType, decimal price, CancellationToken cancellationToken);

    Task<PriceQuoteDto> ResolveAsync(Guid productId, CustomerType customerType, CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceQuoteDto>> ListAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class PriceListRepository(IStoredProcedureExecutor executor) : IPriceListRepository
{
    public async Task<PriceQuoteDto> SetAsync(
        Guid productId,
        CustomerType customerType,
        decimal price,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", productId)
            .With("customer_type", (short)customerType)
            .With("price", price);

        var row = await executor.QueryProcSingleAsync<PriceQuoteRow>(
            "dbo.sp_price_set", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<PriceQuoteDto> ResolveAsync(
        Guid productId,
        CustomerType customerType,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", productId)
            .With("customer_type", (short)customerType);

        var row = await executor.QueryProcSingleAsync<PriceQuoteRow>(
            "dbo.sp_price_resolve", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<IReadOnlyList<PriceQuoteDto>> ListAsync(Guid productId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("product_id", productId);

        var rows = await executor.QueryProcAsync<PriceQuoteRow>(
            "dbo.sp_price_list", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    private sealed class PriceQuoteRow
    {
        public Guid ProductId { get; set; }

        public short CustomerType { get; set; }

        public decimal Price { get; set; }

        public short Source { get; set; }

        public PriceQuoteDto ToDto() => new()
        {
            ProductId = ProductId,
            CustomerType = (CustomerType)CustomerType,
            Price = Price,
            Source = (PriceSource)Source,
        };
    }
}
