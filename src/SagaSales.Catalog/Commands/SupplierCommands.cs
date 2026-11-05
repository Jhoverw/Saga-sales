namespace SagaSales.Catalog.Commands;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

// ── suppliers ───────────────────────────────────────────────────────────────

public record CreateSupplierCommand(CreateSupplierRequest Request) : ICommand<SupplierDto>;

public sealed class CreateSupplierCommandHandler(ISupplierRepository repository)
    : ICommandHandler<CreateSupplierCommand, SupplierDto>
{
    public async Task<Result<SupplierDto>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Request.Name, nameof(request.Request.Name));

        // A duplicate tax id surfaces from the procedure as CONFLICT → 409.
        var supplier = await repository.CreateAsync(request.Request, cancellationToken);
        return Result<SupplierDto>.Success(supplier);
    }
}

public record UpdateSupplierCommand(Guid Id, UpdateSupplierRequest Request) : ICommand<SupplierDto>;

public sealed class UpdateSupplierCommandHandler(ISupplierRepository repository)
    : ICommandHandler<UpdateSupplierCommand, SupplierDto>
{
    public async Task<Result<SupplierDto>> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrEmpty(request.Request.Name, nameof(request.Request.Name));

        var supplier = await repository.UpdateAsync(request.Id, request.Request, cancellationToken);
        return Result<SupplierDto>.Success(supplier);
    }
}

public record DeactivateSupplierCommand(Guid Id) : ICommand;

public sealed class DeactivateSupplierCommandHandler(ISupplierRepository repository)
    : ICommandHandler<DeactivateSupplierCommand>
{
    public async Task<Result> Handle(DeactivateSupplierCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        await repository.DeactivateAsync(request.Id, cancellationToken);
        return Result.Success();
    }
}
