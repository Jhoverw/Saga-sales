namespace SagaSales.Sales.Queries;

using SagaSales.Shared.Time;

/// <summary>
/// Panel "Mi día" (spec 006). <paramref name="OnlyCaller"/> es verdadero para el cajero: entonces el
/// vendedor es siempre quien llama y <paramref name="SellerId"/> se ignora (FR-005).
/// </summary>
public record GetSellerSummaryQuery(LocalDayRange Range, Guid? SellerId, bool OnlyCaller) : IQuery<SellerSalesSummaryDto>;

public sealed class GetSellerSummaryQueryHandler(ISellerSummaryRepository repository, ICurrentUser currentUser)
    : IQueryHandler<GetSellerSummaryQuery, SellerSalesSummaryDto>
{
    public async Task<Result<SellerSalesSummaryDto>> Handle(
        GetSellerSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var sellerId = request.OnlyCaller ? currentUser.RequireUserId() : request.SellerId;

        var daily = await repository.GetDailyAsync(request.Range, sellerId, cancellationToken);
        var products = await repository.GetProductsAsync(request.Range.Today(), sellerId, cancellationToken);
        IReadOnlyList<SellerDto> sellers = request.OnlyCaller
            ? []
            : await repository.GetSellersAsync(request.Range.FromUtc, cancellationToken);

        var byDay = daily.ToDictionary(d => d.Day);
        var days = request.Range.EachDay()
            .Select(day => byDay.TryGetValue(day, out var found) ? found : new DailySalesDto { Day = day })
            .ToList();

        var today = days[^1];

        return Result<SellerSalesSummaryDto>.Success(new SellerSalesSummaryDto
        {
            Days = days,
            TodayProducts = products.ToList(),
            TodayOrders = today.Orders,
            TodayTotal = today.Total,
            Sellers = sellers.ToList(),
        });
    }
}
