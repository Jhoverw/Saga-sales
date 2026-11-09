using Microsoft.Extensions.FileProviders;
using Serilog;
using SagaSales.Catalog.Commands;
using SagaSales.Catalog.Data;
using SagaSales.Catalog.Services;

var builder = WebApplication.CreateBuilder(args);

// One number format everywhere: the point is the decimal separator (constitution v2.4.0).
SagaSales.Shared.Formatting.AppCulture.Apply();

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Catalog");

// Persistence: stored procedures only (constitution §VIII).
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IPriceListRepository, PriceListRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepository>();
builder.Services.AddScoped<IBranchRepository, BranchRepository>();
builder.Services.AddScoped<IInventoryReportRepository, InventoryReportRepository>();
builder.Services.AddSingleton<IProductImageStorage, ProductImageStorage>();

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

// Product pictures are public: the panel loads them in <img> tags (no Authorization header)
// and the storefront will show them to anonymous buyers as well (E-13 · US-026).
var imageStorage = app.Services.GetRequiredService<IProductImageStorage>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorage.RootPath),
    RequestPath = "/images",
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
