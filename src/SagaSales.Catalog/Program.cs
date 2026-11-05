using Serilog;
using SagaSales.Catalog.Commands;
using SagaSales.Catalog.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Catalog");

// Persistence: stored procedures only (constitution §VIII).
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IPriceListRepository, PriceListRepository>();

// Catalog publishes product events; it consumes none of its own.
builder.Services.AddSagaSalesMessageBus(builder.Configuration);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateProductCommandHandler).Assembly));
builder.Services.AddSagaSalesValidation(typeof(CreateProductCommandHandler).Assembly);

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
