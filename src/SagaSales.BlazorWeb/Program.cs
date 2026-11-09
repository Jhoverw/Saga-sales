using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SagaSales.BlazorWeb;
using SagaSales.BlazorWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// One number format everywhere: the point is the decimal separator (constitution v2.4.0).
// Keeps the culture name Blazor started with (es-EC, see index.html): changing it aborts the app.
SagaSales.Shared.Formatting.AppCulture.ApplyToCurrent();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The SPA talks to the YARP gateway, which routes to each service.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000/";

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<TokenStore>();

// The concrete provider is also registered so pages can request it and announce a sign-in:
// resolving only the abstraction would give them a different instance than the cascading state uses.
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();
