namespace SagaSales.BuildingBlocks.EventBus;

using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers MassTransit with the RabbitMQ transport.
/// The previous RabbitMQEventBus (a second, dead IEventBus implementation) was removed:
/// MassTransit is the single bus abstraction of this solution.
/// </summary>
public static class RabbitMqBusRegistration
{
    public static IServiceCollection AddSagaSalesMessageBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddMassTransit(bus =>
        {
            configureConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";
                var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

                cfg.Host(host, virtualHost, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // IPublishEndpoint is scoped: IEventBus must be scoped too (never a singleton).
        services.AddScoped<IEventBus, MassTransitEventBus>();

        return services;
    }
}
