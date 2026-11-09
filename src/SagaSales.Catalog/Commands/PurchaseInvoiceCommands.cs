namespace SagaSales.Catalog.Commands;

using System.Text.RegularExpressions;
using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Exceptions;
using SagaSales.Shared.Purchasing;

/// <summary>
/// Registra la compra tal como viene la factura del proveedor (spec 009): calcula totales y el costo real por
/// unidad con <see cref="PurchaseInvoiceCalculator"/>, guarda la cabecera, las líneas, la cuenta por pagar si es a
/// crédito y, si se pide, recibe la mercadería; todo en una sola transacción.
/// </summary>
public record CreatePurchaseInvoiceCommand(PurchaseInvoiceRequest Request) : ICommand<PurchaseDto>;

public sealed partial class CreatePurchaseInvoiceCommandHandler(
    IPurchaseInvoiceRepository repository,
    ICurrentUser currentUser) : ICommandHandler<CreatePurchaseInvoiceCommand, PurchaseDto>
{
    public async Task<Result<PurchaseDto>> Handle(CreatePurchaseInvoiceCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        Validate(request);

        InvoiceTotals totals;
        try
        {
            totals = PurchaseInvoiceCalculator.Compute(
                request.Lines.Select(l => new InvoiceLineInput(
                    l.Quantity,
                    IsBox(l) ? l.UnitsPerPackage : 1,
                    l.UnitPrice,
                    l.Taxable)).ToList(),
                request.VatRate,
                request.DiscountZero,
                request.DiscountTaxed,
                request.Ice,
                request.OtherCharges);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedException("Se necesita un usuario para registrar la compra.");
        var purchase = await repository.CreateAsync(request, totals, userId, cancellationToken);
        return Result<PurchaseDto>.Success(purchase);
    }

    internal static bool IsBox(PurchaseInvoiceLineRequest line)
        => string.Equals(line.Package, "caja", StringComparison.OrdinalIgnoreCase);

    internal static void Validate(PurchaseInvoiceRequest request)
    {
        if (request.SupplierId == Guid.Empty)
        {
            throw new ValidationException("Elige el proveedor.");
        }

        if (request.DocumentType is < 1 or > 3)
        {
            throw new ValidationException("Tipo de comprobante no válido.");
        }

        request.DocumentNumber = (request.DocumentNumber ?? string.Empty).Trim();
        if (request.DocumentType == 1 && !DocumentNumberPattern().IsMatch(request.DocumentNumber))
        {
            throw new ValidationException("El número de factura va como 001-001-000000123.");
        }

        if (request.DocumentNumber.Length == 0)
        {
            throw new ValidationException("Escribe el número del comprobante.");
        }

        if (request.VatRate is < 0 or >= 1)
        {
            throw new ValidationException("La tasa de IVA va como fracción, por ejemplo 0.15.");
        }

        if (request.IsCredit && request.CreditDueAt is null)
        {
            throw new ValidationException("Una compra a crédito necesita la fecha de vencimiento.");
        }

        if (request.IsCredit && request.CreditDueAt < request.IssuedAt)
        {
            throw new ValidationException("El vencimiento del crédito no puede ser antes de la emisión.");
        }

        if (request.Lines.Count == 0)
        {
            throw new ValidationException("Agrega al menos un producto.");
        }

        foreach (var line in request.Lines)
        {
            if (line.ProductId == Guid.Empty)
            {
                throw new ValidationException($"La línea \"{line.ProductName}\" no tiene producto.");
            }

            if (line.Quantity <= 0 || line.UnitPrice < 0)
            {
                throw new ValidationException($"Revisa cantidad y precio de \"{line.ProductName}\".");
            }

            if (IsBox(line) && line.UnitsPerPackage < 2)
            {
                throw new ValidationException($"Indica cuántas unidades trae la caja de \"{line.ProductName}\".");
            }
        }
    }

    [GeneratedRegex(@"^\d{3}-\d{3}-\d{9}$")]
    private static partial Regex DocumentNumberPattern();
}

/// <summary>Crea un producto desde una línea de la compra: sin stock (lo pone la compra), con su código y empaque.</summary>
public record CreatePurchaseProductCommand(CreatePurchaseProductRequest Request) : ICommand<PurchaseProductDto>;

public sealed class CreatePurchaseProductCommandHandler(
    IPurchaseInvoiceRepository repository,
    IMediator mediator) : ICommandHandler<CreatePurchaseProductCommand, PurchaseProductDto>
{
    public async Task<Result<PurchaseProductDto>> Handle(CreatePurchaseProductCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var name = (request.Name ?? string.Empty).Trim();
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();

        if (name.Length == 0)
        {
            throw new ValidationException("Escribe el nombre del producto.");
        }

        if (request.SalePrice <= 0)
        {
            throw new ValidationException("Pon el precio de venta (PVP).");
        }

        if (request.UnitsPerPackage is < 1)
        {
            throw new ValidationException("Las unidades por caja deben ser 1 o más.");
        }

        if (code is not null && await repository.CodeInUseAsync(code, cancellationToken))
        {
            throw new ConflictException($"El código {code} ya es de otro producto.");
        }

        var created = await mediator.Send(
            new CreateProductCommand(
                name, null, request.SalePrice, "USD", 0, null, request.TracksBatches, request.IsTaxable),
            cancellationToken);

        var product = created.Value!;
        if (code is not null || request.UnitsPerPackage is not null)
        {
            await repository.SetPurchaseInfoAsync(product.Id, code, request.UnitsPerPackage, cancellationToken);
        }

        return Result<PurchaseProductDto>.Success(new PurchaseProductDto
        {
            Id = product.Id,
            Code = code,
            Name = product.Name,
            IsTaxable = request.IsTaxable,
            UnitsPerPackage = request.UnitsPerPackage,
            TracksBatches = request.TracksBatches,
            BasePrice = product.BasePrice,
        });
    }
}

/// <summary>Abono a una cuenta por pagar; el procedure no deja pagar más del saldo.</summary>
public record PayPayableCommand(Guid PayableId, PayablePaymentRequest Request) : ICommand;

public sealed class PayPayableCommandHandler(
    IPurchaseInvoiceRepository repository,
    ICurrentUser currentUser) : ICommandHandler<PayPayableCommand>
{
    public async Task<Result> Handle(PayPayableCommand command, CancellationToken cancellationToken)
    {
        if (command.Request.Amount <= 0)
        {
            throw new ValidationException("El abono debe ser mayor que cero.");
        }

        if (command.Request.Method is not (1 or 2 or 3 or 9))
        {
            throw new ValidationException("Forma de pago no válida.");
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedException("Se necesita un usuario para registrar el abono.");
        await repository.PayAsync(command.PayableId, command.Request, userId, cancellationToken);
        return Result.Success();
    }
}
