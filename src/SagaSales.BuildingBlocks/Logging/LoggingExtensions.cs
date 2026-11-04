namespace SagaSales.BuildingBlocks.Logging;

using Microsoft.AspNetCore.Builder;
using Serilog;

public static class LoggingExtensions
{
    /// <summary>
    /// Configures Serilog for a service. The Seq endpoint is optional and only used when configured.
    /// </summary>
    public static WebApplicationBuilder AddSagaSalesLogging(this WebApplicationBuilder builder, string applicationName)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", applicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

            var seqUrl = context.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                configuration.WriteTo.Seq(seqUrl);
            }
        });

        return builder;
    }
}
