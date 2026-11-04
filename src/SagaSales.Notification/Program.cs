using Serilog;
using SagaSales.Notification.Commands;
using SagaSales.Notification.Data;
using SagaSales.Notification.EventHandlers;
using SagaSales.Notification.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Notification");

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// No provider is configured yet: this sender logs instead of delivering (Phase 2: MailKit/Twilio).
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddScoped<INotificationDispatch, NotificationDispatcher>();

builder.Services.AddSagaSalesMessageBus(builder.Configuration, bus =>
{
    bus.AddConsumer<OrderPlacedConsumer>();
    bus.AddConsumer<HighValueOrderAlertConsumer>();
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SendNotificationCommand).Assembly));
builder.Services.AddSagaSalesValidation(typeof(SendNotificationCommand).Assembly);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck<NotificationDbContext>>("database");

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
