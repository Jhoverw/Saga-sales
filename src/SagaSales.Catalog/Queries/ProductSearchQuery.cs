namespace SagaSales.Catalog.Queries;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

/// <summary>Buscador único de productos (spec 010, HU-10.1 y HU-10.4).</summary>
/// <param name="ForPurchase">Compras: solo bienes. Venta: bienes y servicios.</param>
/// <param name="ShowCost">Solo dueño y administrador ven el costo (T111).</param>
public record SearchProductsQuery(string Term, bool ForPurchase, bool ShowCost, int MaxRows = 10)
    : IQuery<IReadOnlyList<ProductSearchResultDto>>;

public sealed class SearchProductsQueryHandler(IProductCodeRepository repository)
    : IQueryHandler<SearchProductsQuery, IReadOnlyList<ProductSearchResultDto>>
{
    public const int MinTermLength = 2;

    public async Task<Result<IReadOnlyList<ProductSearchResultDto>>> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken)
    {
        var term = (request.Term ?? string.Empty).Trim();
        if (term.Length < MinTermLength)
        {
            return Result<IReadOnlyList<ProductSearchResultDto>>.Success([]);
        }

        var rows = await repository.SearchAsync(
            term[..Math.Min(term.Length, 100)],
            Math.Clamp(request.MaxRows, 1, 50),
            request.ForPurchase ? (byte)2 : (byte)1,
            cancellationToken);

        if (!request.ShowCost)
        {
            foreach (var row in rows)
            {
                row.Cost = null;
            }
        }

        return Result<IReadOnlyList<ProductSearchResultDto>>.Success(rows);
    }
}

public record GetProductBarcodesQuery(Guid ProductId) : IQuery<IReadOnlyList<ProductBarcodeDto>>;

public sealed class GetProductBarcodesQueryHandler(IProductCodeRepository repository)
    : IQueryHandler<GetProductBarcodesQuery, IReadOnlyList<ProductBarcodeDto>>
{
    public async Task<Result<IReadOnlyList<ProductBarcodeDto>>> Handle(
        GetProductBarcodesQuery request,
        CancellationToken cancellationToken)
        => Result<IReadOnlyList<ProductBarcodeDto>>.Success(
            await repository.ListBarcodesAsync(request.ProductId, cancellationToken));
}
