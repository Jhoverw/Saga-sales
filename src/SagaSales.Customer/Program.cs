using Serilog;
using SagaSales.Customer.Commands;
using SagaSales.Customer.Data;

var builder = WebApplication.CreateBuilder(args);

// One number format everywhere: the point is the decimal separator (constitution v2.4.0).
SagaSales.Shared.Formatting.AppCulture.Apply();

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Customer");

// Persistence: stored procedures only (constitution §VIII).
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

builder.Services.AddSagaSalesMessageBus(builder.Configuration);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateCustomerCommandHandler).Assembly));
builder.Services.AddSagaSalesValidation(typeof(CreateCustomerCommandHandler).Assembly);

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
