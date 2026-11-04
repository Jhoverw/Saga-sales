namespace SagaSales.BuildingBlocks.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using SagaSales.BuildingBlocks.Data;

/// <summary>
/// Health check for a service database. It opens a real connection through
/// <see cref="IDbConnectionFactory"/>, so it proves the credentials and the network, not just that
/// an object was constructed.
/// </summary>
public sealed class DatabaseHealthCheck(IDbConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
