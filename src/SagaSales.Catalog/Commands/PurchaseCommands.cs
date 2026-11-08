namespace SagaSales.Catalog.Commands;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

/// <summary>
/// Records a purchase from a supplier and, by default, receives it in the same transaction:
/// the goods and the stock either both land or neither does (decision D13).
/// </summary>
public record CreatePurchaseCommand(
    Guid SupplierId,
    IReadOnlyList<PurchaseLineRequest> Lines,
    Guid? BranchId = null,
    bool ReceiveNow = true,
    string? SupplierInvoiceNumber = null,
    DateTime? PurchasedAt = null,
    string? Notes = null) : ICommand<PurchaseDto>;

public sealed class CreatePurchaseCommandHandler(
    IPurchaseRepository purchases,
    IProductRepository products,
    ICurrentUser currentUser) : ICommandHandler<CreatePurchaseCommand, PurchaseDto>
{
    public async Task<Result<PurchaseDto>> Handle(CreatePurchaseCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.SupplierId, nameof(request.SupplierId));
        Guard.AgainstEmpty(request.Lines, nameof(request.Lines));

        var drafts = new List<PurchaseLineDraft>(request.Lines.Count);

        foreach (var line in request.Lines)
        {
            Guard.AgainstEmptyGuid(line.ProductId, nameof(line.ProductId));

            // Invalid input is a client error (400), not a server failure.
            if (line.Quantity <= 0 || line.UnitCost <= 0)
            {
                throw new SagaSales.Shared.Exceptions.ValidationException(
                    $"La línea del producto {line.ProductId} necesita una cantidad y un costo mayores que cero.");
            }

            var product = await products.GetAsync(line.ProductId, cancellationToken)
                          ?? throw new NotFoundException("Product", line.ProductId);

            drafts.Add(new PurchaseLineDraft(
                product.Id,
                product.Name,
                line.Quantity,
                line.UnitCost,
                line.CostIncludesTax,
                line.TaxRate,
                string.IsNullOrWhiteSpace(line.LotCode) ? null : line.LotCode.Trim(),
                line.ExpiresAt));
        }

        var purchase = await purchases.CreateAsync(
            request.SupplierId,
            string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber) ? null : request.SupplierInvoiceNumber.Trim(),
            request.PurchasedAt,
            request.Notes,
            currentUser.UserId ?? Guid.Empty,
            drafts,
            request.BranchId,
            request.ReceiveNow,
            cancellationToken);

        return Result<PurchaseDto>.Success(purchase);
    }
}

/// <summary>Receives a purchase that was left as a draft. Receiving twice returns 409.</summary>
public record ReceivePurchaseCommand(Guid PurchaseId, Guid? BranchId = null) : ICommand<PurchaseDto>;

public sealed class ReceivePurchaseCommandHandler(
    IPurchaseRepository purchases,
    ICurrentUser currentUser) : ICommandHandler<ReceivePurchaseCommand, PurchaseDto>
{
    public async Task<Result<PurchaseDto>> Handle(ReceivePurchaseCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.PurchaseId, nameof(request.PurchaseId));

        var purchase = await purchases.ReceiveAsync(
            request.PurchaseId, request.BranchId, currentUser.UserId, cancellationToken);

        return Result<PurchaseDto>.Success(purchase);
    }
}
