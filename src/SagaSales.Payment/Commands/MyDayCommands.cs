namespace SagaSales.Payment.Commands;

using SagaSales.Shared.Time;

/// <summary>
/// Cobros del día por forma de pago y caja chica (spec 006). <paramref name="OnlyCaller"/> es verdadero
/// para el cajero: entonces solo cuenta lo que él cobró y gastó (FR-005).
/// </summary>
public record GetPaymentDaySummaryQuery(LocalDayRange Range, Guid? UserId, bool OnlyCaller) : IQuery<PaymentDaySummaryDto>;

public record GetPettyCashQuery(LocalDayRange Range, Guid? UserId, bool OnlyCaller) : IQuery<IReadOnlyList<PettyCashExpenseDto>>;

public record CreatePettyCashExpenseCommand(PettyCashCategory Category, string Description, decimal Amount)
    : ICommand<PettyCashExpenseDto>;

public record VoidPettyCashExpenseCommand(Guid Id) : ICommand<bool>;

public record GetPettyCashSettingsQuery : IQuery<PettyCashSettingsDto>;

public record SetPettyCashSettingsCommand(decimal MaxAmount) : ICommand<PettyCashSettingsDto>;

public sealed class MyDayHandlers(IMyDayRepository repository, ICurrentUser currentUser) :
    IQueryHandler<GetPaymentDaySummaryQuery, PaymentDaySummaryDto>,
    IQueryHandler<GetPettyCashQuery, IReadOnlyList<PettyCashExpenseDto>>,
    ICommandHandler<CreatePettyCashExpenseCommand, PettyCashExpenseDto>,
    ICommandHandler<VoidPettyCashExpenseCommand, bool>,
    IQueryHandler<GetPettyCashSettingsQuery, PettyCashSettingsDto>,
    ICommandHandler<SetPettyCashSettingsCommand, PettyCashSettingsDto>
{
    public async Task<Result<PaymentDaySummaryDto>> Handle(
        GetPaymentDaySummaryQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.OnlyCaller ? currentUser.RequireUserId() : request.UserId;
        var summary = await repository.GetSummaryAsync(request.Range.FromUtc, request.Range.ToUtc, userId, cancellationToken);
        return Result<PaymentDaySummaryDto>.Success(summary);
    }

    public async Task<Result<IReadOnlyList<PettyCashExpenseDto>>> Handle(
        GetPettyCashQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.OnlyCaller ? currentUser.RequireUserId() : request.UserId;
        var rows = await repository.ListPettyCashAsync(request.Range.FromUtc, request.Range.ToUtc, userId, cancellationToken);
        return Result<IReadOnlyList<PettyCashExpenseDto>>.Success(rows);
    }

    public async Task<Result<PettyCashExpenseDto>> Handle(
        CreatePettyCashExpenseCommand request,
        CancellationToken cancellationToken)
    {
        var expense = await repository.CreatePettyCashAsync(
            new CreatePettyCashExpenseRequest
            {
                Category = request.Category,
                Description = request.Description.Trim(),
                Amount = request.Amount,
            },
            currentUser.RequireUserId(),
            currentUser.DisplayName ?? currentUser.Email,
            cancellationToken);

        return Result<PettyCashExpenseDto>.Success(expense);
    }

    public async Task<Result<bool>> Handle(VoidPettyCashExpenseCommand request, CancellationToken cancellationToken)
    {
        await repository.VoidPettyCashAsync(request.Id, currentUser.RequireUserId(), cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<PettyCashSettingsDto>> Handle(
        GetPettyCashSettingsQuery request,
        CancellationToken cancellationToken)
    {
        return Result<PettyCashSettingsDto>.Success(await repository.GetSettingsAsync(cancellationToken));
    }

    public async Task<Result<PettyCashSettingsDto>> Handle(
        SetPettyCashSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await repository.SetSettingsAsync(request.MaxAmount, currentUser.UserId, cancellationToken);
        return Result<PettyCashSettingsDto>.Success(settings);
    }
}
