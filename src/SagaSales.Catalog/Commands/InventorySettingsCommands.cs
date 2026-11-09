namespace SagaSales.Catalog.Commands;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

/// <summary>Saves a product's reorder data without touching price or stock (spec 004, FR-008).</summary>
public record SetInventorySettingsCommand(Guid ProductId, InventorySettingsRequest Request) : ICommand;

public sealed class SetInventorySettingsCommandHandler(IInventoryReportRepository repository)
    : ICommandHandler<SetInventorySettingsCommand>
{
    public async Task<Result> Handle(SetInventorySettingsCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));

        await repository.SetSettingsAsync(request.ProductId, request.Request, cancellationToken);
        return Result.Success();
    }
}
