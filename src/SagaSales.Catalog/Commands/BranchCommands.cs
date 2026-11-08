namespace SagaSales.Catalog.Commands;

using SagaSales.Catalog.Data;
using SagaSales.Shared.DTOs;

// ── branches ────────────────────────────────────────────────────────────────

public record CreateBranchCommand(CreateBranchRequest Request) : ICommand<BranchDto>;

public sealed class CreateBranchCommandHandler(IBranchRepository repository)
    : ICommandHandler<CreateBranchCommand, BranchDto>
{
    public async Task<Result<BranchDto>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Request.Name, nameof(request.Request.Name));

        var branch = await repository.CreateAsync(request.Request, cancellationToken);
        return Result<BranchDto>.Success(branch);
    }
}

public record UpdateBranchCommand(Guid Id, UpdateBranchRequest Request) : ICommand<BranchDto>;

public sealed class UpdateBranchCommandHandler(IBranchRepository repository)
    : ICommandHandler<UpdateBranchCommand, BranchDto>
{
    public async Task<Result<BranchDto>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrEmpty(request.Request.Name, nameof(request.Request.Name));

        var branch = await repository.UpdateAsync(request.Id, request.Request, cancellationToken);
        return Result<BranchDto>.Success(branch);
    }
}

public record DeactivateBranchCommand(Guid Id) : ICommand;

public sealed class DeactivateBranchCommandHandler(IBranchRepository repository)
    : ICommandHandler<DeactivateBranchCommand>
{
    public async Task<Result> Handle(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        await repository.DeactivateAsync(request.Id, cancellationToken);
        return Result.Success();
    }
}

// ── settings ────────────────────────────────────────────────────────────────

public record UpdateSettingsCommand(UpdateSettingsRequest Request) : ICommand<SettingsDto>;

public sealed class UpdateSettingsCommandHandler(IBranchRepository repository)
    : ICommandHandler<UpdateSettingsCommand, SettingsDto>
{
    public async Task<Result<SettingsDto>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        if (request.Request.ExpiryWarningDays is < 1 or > 365)
        {
            throw new SagaSales.Shared.Exceptions.ValidationException(
                "Los días de aviso deben estar entre 1 y 365.");
        }

        var settings = await repository.UpdateSettingsAsync(request.Request, cancellationToken);
        return Result<SettingsDto>.Success(settings);
    }
}

// ── physical location ───────────────────────────────────────────────────────

public record SetLocationCommand(Guid ProductId, SetLocationRequest Request) : ICommand;

public sealed class SetLocationCommandHandler(IBranchRepository repository)
    : ICommandHandler<SetLocationCommand>
{
    public async Task<Result> Handle(SetLocationCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstEmptyGuid(request.ProductId, nameof(request.ProductId));
        Guard.AgainstEmptyGuid(request.Request.BranchId, nameof(request.Request.BranchId));

        await repository.SetLocationAsync(request.ProductId, request.Request, cancellationToken);
        return Result.Success();
    }
}
