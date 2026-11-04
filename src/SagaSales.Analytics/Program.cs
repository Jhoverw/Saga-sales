using Serilog;
using SagaSales.Analytics.Data;
using SagaSales.Analytics.EventHandlers;
using SagaSales.Analytics.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Analytics");

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSagaSalesMessageBus(builder.Configuration, bus =>
{
    bus.AddConsumer<OrderPlacedConsumer>();
    bus.AddConsumer<PaymentAuthorizedConsumer>();
    bus.AddConsumer<CustomerCreatedConsumer>();
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSalesSummaryQuery).Assembly));
builder.Services.AddSagaSalesValidation(typeof(GetSalesSummaryQuery).Assembly);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck<AnalyticsDbContext>>("database");

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
