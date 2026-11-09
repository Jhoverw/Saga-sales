namespace SagaSales.Catalog.Commands;

using SagaSales.Catalog.Data;
using SagaSales.Shared.Catalog;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Exceptions;

/// <summary>
/// Guarda un código de barras en el producto (spec 010, HU-10.2 y HU-10.3). Un código con forma EAN/UPC y
/// dígito verificador incorrecto se guarda igual, con advertencia.
/// </summary>
public record AddProductBarcodeCommand(Guid ProductId, AddProductBarcodeRequest Request) : ICommand<ProductBarcodeDto>;

public sealed class AddProductBarcodeCommandHandler(
    IProductCodeRepository repository,
    ICurrentUser currentUser) : ICommandHandler<AddProductBarcodeCommand, ProductBarcodeDto>
{
    public async Task<Result<ProductBarcodeDto>> Handle(AddProductBarcodeCommand command, CancellationToken cancellationToken)
    {
        var barcode = BarcodeRules.Normalize(command.Request.Barcode);
        if (!BarcodeRules.IsAcceptable(barcode))
        {
            throw new ValidationException(
                $"El código de barras va sin espacios, de {BarcodeRules.MinLength} a {BarcodeRules.MaxLength} caracteres.");
        }

        if (command.Request.Units < 1)
        {
            throw new ValidationException("Las unidades del código deben ser 1 o más.");
        }

        var saved = await repository.AddBarcodeAsync(
            command.ProductId, barcode, command.Request.Units, command.Request.IsPrimary, currentUser.UserId, cancellationToken);

        saved.Warning = BarcodeRules.Warning(barcode);
        return Result<ProductBarcodeDto>.Success(saved);
    }
}

public record RemoveProductBarcodeCommand(Guid ProductId, Guid BarcodeId) : ICommand;

public sealed class RemoveProductBarcodeCommandHandler(IProductCodeRepository repository)
    : ICommandHandler<RemoveProductBarcodeCommand>
{
    public async Task<Result> Handle(RemoveProductBarcodeCommand command, CancellationToken cancellationToken)
    {
        await repository.RemoveBarcodeAsync(command.ProductId, command.BarcodeId, cancellationToken);
        return Result.Success();
    }
}
