using AgroAdmin.Components;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Контроллеры из AgroAdmin.API
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AgroAdmin.API.Controllers.AuthController).Assembly);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// 2. БД
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. HttpClient
var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:8080";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(frontendUrl) });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();

// 4. Куки
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "AgroAdmin.Auth";
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// 5. Твой Телеграм (Singleton как ты просил ранее)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITelegramService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<TelegramService>>();
    return new TelegramService(httpClientFactory, configuration, logger);
});

// 6. Защита ключей (для Docker)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("AgroAdmin");

var app = builder.Build();

// Миграции
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// 7. Pipeline (ПОРЯДОК КРИТИЧЕН)
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseStaticFiles(); // БЕЗ ЭТОГО БУДЕТ 404
app.MapStaticAssets(); // Для .NET 10

app.UseRouting();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Редирект корня
app.MapGet("/", context => {
    context.Response.Redirect("/calendar");
    return Task.CompletedTask;
});

// Маппинг Blazor
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AgroAdmin.Client._Imports).Assembly);

app.Run();
