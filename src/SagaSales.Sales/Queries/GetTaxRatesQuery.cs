namespace SagaSales.Sales.Queries;

using SagaSales.Shared;
using SagaSales.Shared.DTOs;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

public record GetTaxRatesQuery() : IQuery<IReadOnlyList<TaxRateDto>>;

public sealed class GetTaxRatesQueryHandler(IConfiguration config, ICurrentUser currentUser)
    : IQueryHandler<GetTaxRatesQuery, IReadOnlyList<TaxRateDto>>
{
    public async Task<Result<IReadOnlyList<TaxRateDto>>> Handle(GetTaxRatesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();
        var connectionString = config.GetConnectionString("DefaultConnection")!;
        using var conn = new SqlConnection(connectionString);

        // §V: the tenant filter is part of the query, never optional.
        const string sql =
            "SELECT id, rate_percentage AS RatePercentage, start_date AS StartDate, " +
            "       end_date AS EndDate, description AS Description " +
            "FROM tax_rates WHERE tenant_id = @TenantId ORDER BY start_date DESC";

        var rates = await conn.QueryAsync<TaxRateDto>(sql, new { TenantId = tenantId });

        return Result<IReadOnlyList<TaxRateDto>>.Success(rates.ToList());
    }
}
