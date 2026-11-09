namespace SagaSales.Catalog.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;

/// <summary>Buscador único y códigos de barras de los productos (spec 010), vía <c>db/catalog/V027</c>.</summary>
public interface IProductCodeRepository
{
    /// <param name="purpose">1 venta (bienes y servicios) · 2 compra (solo bienes).</param>
    Task<IReadOnlyList<ProductSearchResultDto>> SearchAsync(string term, int maxRows, byte purpose, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductBarcodeDto>> ListBarcodesAsync(Guid productId, CancellationToken cancellationToken);

    Task<ProductBarcodeDto> AddBarcodeAsync(Guid productId, string barcode, int units, bool isPrimary, Guid? createdBy, CancellationToken cancellationToken);

    Task RemoveBarcodeAsync(Guid productId, Guid barcodeId, CancellationToken cancellationToken);
}

public sealed class ProductCodeRepository(IStoredProcedureExecutor executor) : IProductCodeRepository
{
    public async Task<IReadOnlyList<ProductSearchResultDto>> SearchAsync(
        string term, int maxRows, byte purpose, CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<ProductSearchResultDto>(
            "dbo.sp_product_search",
            SqlArgs.New().With("term", term).With("max_rows", maxRows).With("purpose", purpose),
            cancellationToken: cancellationToken);

        foreach (var row in rows)
        {
            row.CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
            row.UpdatedAt = DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc);
        }

        return rows;
    }

    public Task<IReadOnlyList<ProductBarcodeDto>> ListBarcodesAsync(Guid productId, CancellationToken cancellationToken)
        => executor.QueryProcAsync<ProductBarcodeDto>(
            "dbo.sp_product_barcode_list",
            SqlArgs.New().With("product_id", productId),
            cancellationToken: cancellationToken);

    public async Task<ProductBarcodeDto> AddBarcodeAsync(
        Guid productId, string barcode, int units, bool isPrimary, Guid? createdBy, CancellationToken cancellationToken)
    {
        // CONFLICT (código de otro producto), VALIDATION_ERROR y NOT_FOUND vienen del procedure.
        var row = await executor.QueryProcSingleAsync<ProductBarcodeDto>(
            "dbo.sp_product_barcode_add",
            SqlArgs.New()
                .With("product_id", productId)
                .With("barcode", barcode)
                .With("units", units)
                .With("is_primary", isPrimary)
                .With("created_by", createdBy),
            cancellationToken: cancellationToken);

        return row!;
    }

    public Task RemoveBarcodeAsync(Guid productId, Guid barcodeId, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_product_barcode_remove",
            SqlArgs.New().With("product_id", productId).With("barcode_id", barcodeId),
            cancellationToken: cancellationToken);
}
