using System.Globalization;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RegistroTrabajo;
using RegistroTrabajo.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var cultura = new CultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var apiUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? throw new InvalidOperationException("Falta ApiSettings:BaseUrl en wwwroot/appsettings.json");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));

// Sesión y autenticación de la interfaz
builder.Services.AddScoped<SesionStore>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthProvider>();
builder.Services.AddAuthorizationCore();

// Cliente "Api" (con JWT y renovación automática) y "ApiAnon" (sin handler, para refresh)
builder.Services.AddScoped<AuthHttpHandler>();
builder.Services.AddHttpClient("Api", c => c.BaseAddress = new Uri(apiUrl))
    .AddHttpMessageHandler<AuthHttpHandler>();
builder.Services.AddHttpClient(AuthHttpHandler.ClienteAnonimo, c => c.BaseAddress = new Uri(apiUrl));

builder.Services.AddScoped(sp => new ApiClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"),
    sp.GetRequiredService<JsonSerializerOptions>(),
    sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()));
builder.Services.AddScoped<AuthClient>();
builder.Services.AddScoped<InicioClient>();
builder.Services.AddScoped<UsuariosClient>();
builder.Services.AddScoped<RolesClient>();
builder.Services.AddScoped<SesionesClient>();
builder.Services.AddScoped<RegistrosClient>();
builder.Services.AddScoped<EmpresaClient>();
builder.Services.AddScoped<PlataformaClient>();
builder.Services.AddScoped<CatalogosClient>();
builder.Services.AddScoped<TrabajadoresClient>();
builder.Services.AddScoped<SolicitudesPerfilClient>();

await builder.Build().RunAsync();
