namespace SagaSales.BuildingBlocks.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SagaSales.BuildingBlocks.Data;
using SagaSales.BuildingBlocks.Security;

/// <summary>
/// One-call wiring of the shared infrastructure every service needs.
/// </summary>
public static class InfrastructureExtensions
{
    public static IServiceCollection AddSagaSalesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddHealthChecks();
        services.AddSagaSalesAuthentication(configuration);

        // Persistence: stored procedures only (constitution §VIII).
        services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();

        return services;
    }
}
