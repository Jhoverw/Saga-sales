using Serilog;
using SagaSales.Payment.Commands;
using SagaSales.Payment.Data;
using SagaSales.Payment.EventHandlers;
using SagaSales.Payment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Payment");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// The stub gateway authorizes without moving money. Phase 2 swaps in a real provider.
builder.Services.AddScoped<IPaymentGateway, StubPaymentGateway>();

builder.Services.AddSagaSalesMessageBus(builder.Configuration, bus =>
    bus.AddConsumer<OrderPlacedConsumer>());

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreatePaymentCommandHandler).Assembly));
builder.Services.AddSagaSalesValidation(typeof(CreatePaymentCommandHandler).Assembly);

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
