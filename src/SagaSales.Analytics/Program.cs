using Serilog;
using SagaSales.Analytics.Data;
using SagaSales.Analytics.EventHandlers;
using SagaSales.Analytics.Queries;
using SagaSales.Analytics.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Analytics");
builder.Services.AddScoped<ISalesSummaryCsvExporter, SalesSummaryCsvExporter>();


// Persistence: stored procedures only (constitution §VIII).
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

builder.Services.AddSagaSalesMessageBus(builder.Configuration, bus =>
{
    // Own queue per service: the default endpoint name for OrderPlacedConsumer collides with
    // Payment and Notification, turning a fan-out into competing consumers that drop 2 of 3 events.
    bus.AddConsumer<OrderPlacedConsumer>()
        .Endpoint(e => e.Name = "analytics-order-placed");
    bus.AddConsumer<PaymentAuthorizedConsumer>();
    bus.AddConsumer<CustomerCreatedConsumer>();
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSalesSummaryQuery).Assembly));
builder.Services.AddSagaSalesValidation(typeof(GetSalesSummaryQuery).Assembly);

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
