using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSagaSalesLogging(SagaSalesConstants.ApplicationName);

builder.Services.AddSagaSalesInfrastructure(builder.Configuration);
builder.Services.AddSagaSalesTelemetry(builder.Configuration, "SagaSales.ApiGateway");

// YARP reads the "ReverseProxy" section (routes + clusters, see appsettings.json).
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options => options.AddPolicy("blazor", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5008"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSagaSalesExceptionHandling();
app.UseCors("blazor");

app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// YARP proxies every route declared in configuration. The previous code registered a
// redirect from /api/catalog to /api/catalog, which looped forever.
app.MapReverseProxy();

app.Run();
