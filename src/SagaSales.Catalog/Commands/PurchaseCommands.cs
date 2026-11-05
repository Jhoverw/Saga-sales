namespace SagaSales.Catalog.Commands;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

/// <summary>
/// Records a purchase from a supplier. The product name is resolved here so the line keeps a
/// snapshot: if the product is renamed later, the purchase still shows what was bought.
/// </summary>
public record CreatePurchaseCommand(
    Guid SupplierId,
    IReadOnlyList<PurchaseLineRequest> Lines,
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
            Guard.AgainstNegativeOrZero(line.Quantity, $"Quantity for product {line.ProductId}");
            Guard.AgainstNegativeOrZero(line.UnitCost, $"Cost for product {line.ProductId}");

            var product = await products.GetAsync(line.ProductId, cancellationToken)
                          ?? throw new NotFoundException("Product", line.ProductId);

            drafts.Add(new PurchaseLineDraft(
                product.Id,
                product.Name,
                line.Quantity,
                line.UnitCost,
                line.CostIncludesTax,
                line.TaxRate));
        }

        var purchase = await purchases.CreateAsync(
            request.SupplierId,
            string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber) ? null : request.SupplierInvoiceNumber.Trim(),
            request.PurchasedAt,
            request.Notes,
            currentUser.UserId ?? Guid.Empty,
            drafts,
            cancellationToken);

        return Result<PurchaseDto>.Success(purchase);
    }
}

/// <summary>
/// Receives a purchase: brings the stock in, recalculates the weighted average cost and writes the
/// kardex. Receiving twice returns 409 (decision D13).
/// </summary>
public record ReceivePurchaseCommand(Guid PurchaseId) : ICommand<PurchaseDto>;

public sealed class ReceivePurchaseCommandHandler(
    IPurchaseRepository purchases,
    ICurrentUser currentUser) : ICommandHandler<ReceivePurchaseCommand, PurchaseDto>
{
    public async Task<Result<PurchaseDto>> Handle(ReceivePurchaseCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.PurchaseId, nameof(request.PurchaseId));

        var purchase = await purchases.ReceiveAsync(request.PurchaseId, currentUser.UserId, cancellationToken);
        return Result<PurchaseDto>.Success(purchase);
    }
}
