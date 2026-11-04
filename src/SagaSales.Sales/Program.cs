using Serilog;
using SagaSales.Sales.Commands;
using SagaSales.Sales.Data;
using SagaSales.Sales.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.Sales");

builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Catalog is reached over HTTP; Sales no longer references Catalog's DbContext.
builder.Services.AddTransient<ForwardCredentialsHandler>();
builder.Services
    .AddHttpClient<ICatalogClient, CatalogClient>(http =>
        http.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5002"))
    .AddHttpMessageHandler<ForwardCredentialsHandler>();

builder.Services.AddSagaSalesMessageBus(builder.Configuration);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommandHandler).Assembly));
builder.Services.AddSagaSalesValidation(typeof(CreateOrderCommandHandler).Assembly);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck<SalesDbContext>>("database");

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
