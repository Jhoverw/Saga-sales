namespace SagaSales.BuildingBlocks.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

/// <summary>
/// OpenTelemetry wiring shared by every service. Previously this file held a full
/// <c>Program.cs</c> (top-level statements) inside this class library, which cannot compile.
/// </summary>
public static class TelemetryExtensions
{
    public static IServiceCollection AddSagaSalesTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        return services;
    }
}
