namespace SagaSales.Sales.Queries;

using SagaSales.Shared;
using SagaSales.Shared.DTOs;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

/// <summary>V-03: the VAT rate in force for a given date, resolved by sp_tax_rate_for_date.</summary>
public record GetCurrentTaxRateQuery(DateOnly Date) : IQuery<TaxRateDto?>;

public sealed class GetCurrentTaxRateQueryHandler(IConfiguration config, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentTaxRateQuery, TaxRateDto?>
{
    public async Task<Result<TaxRateDto?>> Handle(GetCurrentTaxRateQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();
        var connectionString = config.GetConnectionString("DefaultConnection")!;
        using var conn = new SqlConnection(connectionString);

        // §V: the tenant is a parameter of the procedure, never optional.
        var rate = await conn.QuerySingleOrDefaultAsync<TaxRateDto>(
            "dbo.sp_tax_rate_for_date",
            new
            {
                p_tenant_id = tenantId,
                rate_date = request.Date.ToDateTime(TimeOnly.MinValue),
            },
            commandType: CommandType.StoredProcedure);

        return Result<TaxRateDto?>.Success(rate);
    }
}
