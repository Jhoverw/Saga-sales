using Serilog;
using SagaSales.Payment.Commands;
using SagaSales.Payment.Data;
using SagaSales.Payment.EventHandlers;
using SagaSales.Payment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// One number format everywhere: the point is the decimal separator (constitution v2.4.0).
SagaSales.Shared.Formatting.AppCulture.Apply();

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Payment");

// Persistence: stored procedures only (constitution §VIII).
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

// Sales owns orders; Payment asks it to reflect the money received.
builder.Services.AddTransient<ForwardIdentityHandler>();
builder.Services
    .AddHttpClient<IOrderingClient, OrderingClient>(http =>
        http.BaseAddress = new Uri(builder.Configuration["Services:Sales"] ?? "http://localhost:5003"))
    .AddHttpMessageHandler<ForwardIdentityHandler>();

builder.Services.AddSagaSalesMessageBus(
    builder.Configuration,
    bus => bus.AddConsumer<OrderPlacedConsumer>()
        .Endpoint(e => e.Name = "payment-order-placed"));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreatePaymentCommandHandler).Assembly));
builder.Services.AddSagaSalesValidation(typeof(CreatePaymentCommandHandler).Assembly);

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
