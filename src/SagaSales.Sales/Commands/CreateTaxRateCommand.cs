namespace SagaSales.Sales.Commands;

using SagaSales.Shared;
using SagaSales.Shared.DTOs;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

public record CreateTaxRateCommand(
    decimal RatePercentage,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description) : ICommand<int>;

public sealed class CreateTaxRateCommandHandler(IConfiguration config, ICurrentUser currentUser)
    : ICommandHandler<CreateTaxRateCommand, int>
{
    public async Task<Result<int>> Handle(CreateTaxRateCommand request, CancellationToken cancellationToken)
    {
        if (request.RatePercentage <= 0 || request.RatePercentage > 1)
        {
            throw new SagaSales.Shared.Exceptions.ValidationException(
                "El porcentaje de impuesto debe estar entre 0 y 1 (0.15 = 15 %).");
        }

        var tenantId = currentUser.RequireTenantId();
        var connectionString = config.GetConnectionString("DefaultConnection")!;
        using var conn = new SqlConnection(connectionString);

        const string sql = @"
            INSERT INTO tax_rates (tenant_id, rate_percentage, start_date, end_date, description)
            VALUES (@TenantId, @RatePercentage, @StartDate, @EndDate, @Description);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var id = await conn.ExecuteScalarAsync<int>(sql, new
        {
            TenantId = tenantId,
            request.RatePercentage,
            StartDate = request.StartDate.ToDateTime(TimeOnly.MinValue),
            EndDate = request.EndDate?.ToDateTime(TimeOnly.MinValue),
            request.Description,
        });

        return Result<int>.Success(id);
    }
}
