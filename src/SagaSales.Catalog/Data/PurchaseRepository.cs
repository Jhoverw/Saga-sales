namespace SagaSales.Catalog.Data;

using System.Text.Json;
using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;

/// <summary>
/// Purchases and the kardex, through <c>db/catalog/V006__functions_purchasing.sql</c>.
/// Receiving is the only operation that brings stock in and recalculates the weighted cost.
/// </summary>
public interface IPurchaseRepository
{
    Task<PurchaseDto> CreateAsync(
        Guid supplierId,
        string? supplierInvoiceNumber,
        DateTime? purchasedAt,
        string? notes,
        Guid createdBy,
        IReadOnlyList<PurchaseLineDraft> lines,
        CancellationToken cancellationToken);

    /// <summary>Receives the purchase: stock in, weighted cost and kardex.</summary>
    Task<PurchaseDto> ReceiveAsync(Guid purchaseId, Guid? receivedBy, CancellationToken cancellationToken);

    Task<PurchaseDto?> GetAsync(Guid purchaseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseLineDto>> GetLinesAsync(Guid purchaseId, CancellationToken cancellationToken);

    Task<PurchaseListResult> ListAsync(int pageNumber, int pageSize, short? status, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(Guid productId, int maxRows, CancellationToken cancellationToken);
}

/// <summary>One line as it is handed to the procedure, with the product name already resolved.</summary>
public sealed record PurchaseLineDraft(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitCost,
    bool CostIncludesTax,
    decimal? TaxRate);

public sealed record PurchaseListResult(IReadOnlyList<PurchaseDto> Items, int TotalCount);

public sealed class PurchaseRepository(
    IStoredProcedureExecutor executor,
    ILogger<PurchaseRepository> logger) : IPurchaseRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PurchaseDto> CreateAsync(
        Guid supplierId,
        string? supplierInvoiceNumber,
        DateTime? purchasedAt,
        string? notes,
        Guid createdBy,
        IReadOnlyList<PurchaseLineDraft> lines,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            lines.Select(l => new
            {
                productId = l.ProductId,
                productName = l.ProductName,
                quantity = l.Quantity,
                unitCost = l.UnitCost,
                costIncludesTax = l.CostIncludesTax,
                taxRate = l.TaxRate,
            }),
            Json);

        var arguments = SqlArgs.New()
            .With("supplier_id", supplierId)
            .With("created_by", createdBy)
            .With("lines_json", payload)
            .With("supplier_invoice_number", supplierInvoiceNumber)
            .With("purchased_at", purchasedAt)
            .With("notes", notes);

        var row = await executor.QueryProcSingleAsync<PurchaseRow>(
            "dbo.sp_purchase_create", arguments, cancellationToken: cancellationToken);

        logger.LogInformation("Purchase {PurchaseId} created with {Lines} line(s)", row!.Id, lines.Count);
        return row.ToDto(lines);
    }

    public async Task<PurchaseDto> ReceiveAsync(
        Guid purchaseId,
        Guid? receivedBy,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("purchase_id", purchaseId)
            .With("received_by", receivedBy);

        // INVALID_TRANSITION comes from the procedure and maps to 409 (already received).
        var row = await executor.QueryProcSingleAsync<PurchaseRow>(
            "dbo.sp_purchase_receive", arguments, cancellationToken: cancellationToken);

        logger.LogInformation("Purchase {PurchaseId} received: stock and weighted cost updated", purchaseId);
        return row!.ToDto([]);
    }

    public async Task<PurchaseDto?> GetAsync(Guid purchaseId, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("purchase_id", purchaseId);

        var row = await executor.QueryProcSingleAsync<PurchaseRow>(
            "dbo.sp_purchase_get", arguments, cancellationToken: cancellationToken);

        return row?.ToDto([]);
    }

    public async Task<IReadOnlyList<PurchaseLineDto>> GetLinesAsync(
        Guid purchaseId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("purchase_id", purchaseId);

        var rows = await executor.QueryProcAsync<PurchaseLineRow>(
            "dbo.sp_purchase_lines", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<PurchaseListResult> ListAsync(
        int pageNumber,
        int pageSize,
        short? status,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("page_number", pageNumber)
            .With("page_size", pageSize)
            .With("status", status);

        var rows = await executor.QueryProcAsync<PurchaseRow>(
            "dbo.sp_purchase_list", arguments, cancellationToken: cancellationToken);

        var items = rows.Select(r => r.ToDto([])).ToList();
        var total = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new PurchaseListResult(items, total);
    }

    public async Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(
        Guid productId,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", productId)
            .With("max_rows", maxRows);

        var rows = await executor.QueryProcAsync<StockMovementRow>(
            "dbo.sp_stock_movement_list", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    private sealed class PurchaseRow
    {
        public Guid Id { get; set; }

        public Guid SupplierId { get; set; }

        public string? SupplierName { get; set; }

        public short Status { get; set; }

        public string? SupplierInvoiceNumber { get; set; }

        public DateTime PurchasedAt { get; set; }

        public DateTime? ReceivedAt { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal Total { get; set; }

        public string? Notes { get; set; }

        public int TotalCount { get; set; }

        public PurchaseDto ToDto(IEnumerable<PurchaseLineDraft> lines) => new()
        {
            Id = Id,
            SupplierId = SupplierId,
            SupplierName = SupplierName,
            Status = Status,
            SupplierInvoiceNumber = SupplierInvoiceNumber,
            PurchasedAt = PurchasedAt,
            ReceivedAt = ReceivedAt,
            Subtotal = Subtotal,
            TaxAmount = TaxAmount,
            Total = Total,
            Notes = Notes,
            Lines = lines.Select(l => new PurchaseLineDto
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                CostIncludesTax = l.CostIncludesTax,
                TaxRate = l.TaxRate,

                // Mirrors the rule applied in SQL: the effective cost is always tax-included.
                EffectiveUnitCost = l.CostIncludesTax || l.TaxRate is null
                    ? l.UnitCost
                    : Math.Round(l.UnitCost * (1 + (l.TaxRate.Value / 100m)), 4),
            }).ToList(),
        };
    }

    private sealed class PurchaseLineRow
    {
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public bool CostIncludesTax { get; set; }

        public decimal? TaxRate { get; set; }

        public decimal EffectiveUnitCost { get; set; }

        public decimal LineTotal { get; set; }

        public PurchaseLineDto ToDto() => new()
        {
            ProductId = ProductId,
            ProductName = ProductName,
            Quantity = Quantity,
            UnitCost = UnitCost,
            CostIncludesTax = CostIncludesTax,
            TaxRate = TaxRate,
            EffectiveUnitCost = EffectiveUnitCost,
            LineTotal = LineTotal,
        };
    }

    private sealed class StockMovementRow
    {
        public Guid Id { get; set; }

        public short Kind { get; set; }

        public int Quantity { get; set; }

        public decimal? UnitCost { get; set; }

        public int StockAfter { get; set; }

        public string? ReferenceType { get; set; }

        public Guid? ReferenceId { get; set; }

        public DateTime OccurredAt { get; set; }

        public StockMovementDto ToDto() => new()
        {
            Id = Id,
            Kind = Kind,
            Quantity = Quantity,
            UnitCost = UnitCost,
            StockAfter = StockAfter,
            ReferenceType = ReferenceType,
            ReferenceId = ReferenceId,
            OccurredAt = OccurredAt,
        };
    }
}
