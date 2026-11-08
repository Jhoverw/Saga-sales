using Serilog;
using SagaSales.Notification.Data;
using SagaSales.Notification.EventHandlers;
using SagaSales.Notification.Infrastructure;
using SagaSales.Notification.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Notification");

// Persistence: stored procedures only (constitution §VIII).
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddScoped<INotificationDispatch, NotificationDispatcher>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddSagaSalesMessageBus(builder.Configuration, bus =>
{
    // Dedicated queue: OrderPlaced must not compete with Analytics/Payment.
    bus.AddConsumer<OrderConfirmationConsumer>()
        .Endpoint(e => e.Name = "notification-order-placed");
    bus.AddConsumer<HighValueOrderAlertConsumer>();
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(NotificationService).Assembly));
builder.Services.AddSagaSalesValidation(typeof(NotificationService).Assembly);

builder.Services.AddControllers();

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSagaSalesExceptionHandling();
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
