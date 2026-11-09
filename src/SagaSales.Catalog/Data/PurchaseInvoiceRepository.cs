namespace SagaSales.Catalog.Data;

using System.Text.Json;
using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Purchasing;

/// <summary>Compras como factura y cuentas por pagar (spec 009), vía <c>db/catalog/V026</c>.</summary>
public interface IPurchaseInvoiceRepository
{
    Task<PurchaseDto> CreateAsync(
        PurchaseInvoiceRequest request,
        InvoiceTotals totals,
        Guid createdBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseProductDto>> FindProductsAsync(string term, int maxRows, CancellationToken cancellationToken);

    Task SetPurchaseInfoAsync(Guid productId, string? code, int? unitsPerPackage, CancellationToken cancellationToken);

    Task<bool> CodeInUseAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayableDto>> ListPayablesAsync(bool includePaid, CancellationToken cancellationToken);

    Task PayAsync(Guid payableId, PayablePaymentRequest request, Guid createdBy, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayablePaymentDto>> ListPaymentsAsync(Guid payableId, CancellationToken cancellationToken);
}

public sealed class PurchaseInvoiceRepository(IStoredProcedureExecutor executor) : IPurchaseInvoiceRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PurchaseDto> CreateAsync(
        PurchaseInvoiceRequest request,
        InvoiceTotals totals,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        var lines = request.Lines.Select((line, i) =>
        {
            var computed = totals.Lines[i];
            var isBox = string.Equals(line.Package, "caja", StringComparison.OrdinalIgnoreCase);
            return new
            {
                productId = line.ProductId,
                productName = line.ProductName.Trim(),
                stockQuantity = computed.StockQuantity,
                invoiceQuantity = line.Quantity,
                packageLabel = isBox ? $"caja x{line.UnitsPerPackage}" : "unidad",
                unitsPerPackage = isBox ? line.UnitsPerPackage : 1,
                invoiceUnitPrice = line.UnitPrice,
                discount = computed.Discount,
                taxable = line.Taxable,
                effectiveUnitCost = computed.EffectiveUnitCost,
                lineTotal = computed.LineTotal,
                lotCode = string.IsNullOrWhiteSpace(line.LotCode) ? null : line.LotCode.Trim(),
                expiresAt = line.ExpiresAt?.ToString("yyyy-MM-dd"),
                savePackage = isBox,
            };
        });

        var arguments = SqlArgs.New()
            .With("supplier_id", request.SupplierId)
            .With("created_by", createdBy)
            .With("branch_id", request.BranchId)
            .With("receive", request.ReceiveNow)
            .With("document_type", (short)request.DocumentType)
            .With("document_number", request.DocumentNumber)
            .With("authorization_number", request.AuthorizationNumber)
            .With("issued_at", request.IssuedAt.ToDateTime(TimeOnly.MinValue))
            .With("received_at", request.ReceivedAt?.ToUniversalTime())
            .With("document_expires_at", request.DocumentExpiresAt?.ToDateTime(TimeOnly.MinValue))
            .With("payment_method", request.PaymentMethod is { } method ? (short?)method : null)
            .With("is_credit", request.IsCredit)
            .With("credit_due_at", request.CreditDueAt?.ToDateTime(TimeOnly.MinValue))
            .With("vat_rate", request.VatRate)
            .With("subtotal_zero", totals.SubtotalZero)
            .With("subtotal_taxed", totals.SubtotalTaxed)
            .With("discount_zero", totals.DiscountZero)
            .With("discount_taxed", totals.DiscountTaxed)
            .With("ice_amount", totals.Ice)
            .With("tax_amount", totals.Tax)
            .With("other_charges", totals.Others)
            .With("total", totals.Total)
            .With("notes", request.Notes)
            .With("lines_json", JsonSerializer.Serialize(lines, Json));

        // CONFLICT (comprobante repetido), VALIDATION_ERROR y NOT_FOUND vienen del procedure.
        var row = await executor.QueryProcSingleAsync<CreatedRow>(
            "dbo.sp_purchase_create_invoice", arguments, cancellationToken: cancellationToken);

        return new PurchaseDto
        {
            Id = row!.Id,
            SupplierId = row.SupplierId,
            BranchId = row.BranchId,
            Status = row.Status,
            SupplierInvoiceNumber = row.SupplierInvoiceNumber,
            PurchasedAt = DateTime.SpecifyKind(row.PurchasedAt, DateTimeKind.Utc),
            ReceivedAt = row.ReceivedAt is { } received ? DateTime.SpecifyKind(received, DateTimeKind.Utc) : null,
            Subtotal = row.Subtotal,
            TaxAmount = row.TaxAmount,
            Total = row.Total,
            Notes = row.Notes,
        };
    }

    public async Task<IReadOnlyList<PurchaseProductDto>> FindProductsAsync(string term, int maxRows, CancellationToken cancellationToken)
        => (await executor.QueryProcAsync<PurchaseProductDto>(
               "dbo.sp_purchase_product_find",
               SqlArgs.New().With("term", term).With("max_rows", maxRows),
               cancellationToken: cancellationToken)).ToList();

    public Task SetPurchaseInfoAsync(Guid productId, string? code, int? unitsPerPackage, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_product_set_purchase_info",
            SqlArgs.New().With("product_id", productId).With("code", code).With("units_per_package", unitsPerPackage),
            cancellationToken: cancellationToken);

    public async Task<bool> CodeInUseAsync(string code, CancellationToken cancellationToken)
    {
        var found = await FindProductsAsync(code, 5, cancellationToken);
        return found.Any(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<PayableDto>> ListPayablesAsync(bool includePaid, CancellationToken cancellationToken)
        => (await executor.QueryProcAsync<PayableDto>(
               "dbo.sp_payable_list",
               SqlArgs.New().With("include_paid", includePaid),
               cancellationToken: cancellationToken)).ToList();

    public Task PayAsync(Guid payableId, PayablePaymentRequest request, Guid createdBy, CancellationToken cancellationToken)
        => executor.ExecuteProcAsync(
            "dbo.sp_payable_pay",
            SqlArgs.New()
                .With("payable_id", payableId)
                .With("amount", request.Amount)
                .With("paid_at", request.PaidAt.ToDateTime(TimeOnly.MinValue))
                .With("method", (short)request.Method)
                .With("reference", request.Reference)
                .With("created_by", createdBy),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<PayablePaymentDto>> ListPaymentsAsync(Guid payableId, CancellationToken cancellationToken)
        => (await executor.QueryProcAsync<PayablePaymentDto>(
               "dbo.sp_payable_payments_list",
               SqlArgs.New().With("payable_id", payableId),
               cancellationToken: cancellationToken)).ToList();

    private sealed class CreatedRow
    {
        public Guid Id { get; set; }

        public Guid SupplierId { get; set; }

        public Guid? BranchId { get; set; }

        public short Status { get; set; }

        public string? SupplierInvoiceNumber { get; set; }

        public DateTime PurchasedAt { get; set; }

        public DateTime? ReceivedAt { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal Total { get; set; }

        public string? Notes { get; set; }
    }
}
