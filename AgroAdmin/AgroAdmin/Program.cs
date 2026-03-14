using AgroAdmin.Components;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.BackgroundServices;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.Shared.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ===== РЕГИСТРАЦИЯ СЕРВИСОВ =====
// ПОРЯДОК ВАЖЕН: Логирование → База → Безопасность → Сервисы

// 1. ЛОГИРОВАНИЕ
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// 2. БАЗА И КОНТРОЛЛЕРЫ
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AgroAdmin.API.Controllers.AuthController).Assembly);
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// 3. БЕЗОПАСНОСТЬ
builder.Services.AddCors(options => {
    options.AddPolicy("AllowSpecificOrigin", policy => {
        policy.WithOrigins("https://agroadmin.runasp.net", "http://localhost:8080")
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

// 3.1 Аутентификация (SameSite=None для Safari, 7 дней)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "AgroAdmin.Auth";
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = 401;
                }
                else
                {
                    ctx.Response.Redirect(ctx.RedirectUri);
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// 3.2 Data Protection (общие ключи для всех подов)
try
{
    var keysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys");
    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("AgroAdmin");
    Log.Information("Data Protection keys: {KeysPath}", keysPath);
}
catch (Exception ex)
{
    Log.Warning(ex, "Using ephemeral Data Protection keys");
    builder.Services.AddDataProtection().SetApplicationName("AgroAdmin");
}

// 4. СЕРВИСЫ ПРИЛОЖЕНИЯ
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingValidationService, BookingValidationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddScoped<BookingFormService>();
builder.Services.AddHostedService<DatabaseScannerService>();

// 5. MASSTRANSIT (RabbitMQ)
builder.Services.AddMassTransit(x => {
    x.AddConsumer<BookingCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) => {
        var rabbitUrl = builder.Configuration["RabbitMQ:Url"]
                        ?? builder.Configuration["RabbitMQ__Url"]
                        ?? Environment.GetEnvironmentVariable("RabbitMQ__Url");
        if (!string.IsNullOrEmpty(rabbitUrl))
        {
            cfg.Host(new Uri(rabbitUrl.Trim().TrimEnd('/')));
            cfg.ConfigureEndpoints(context);
        }
    });
});

var app = builder.Build();

// ===== КОНВЕЙЕР ЗАПРОСОВ =====
// ПОРЯДОК КРИТИЧЕН: Ошибки → Статика → Маршрутизация → Авторизация → Endpoints

// 6. МИГРАЦИИ (при старте)
using (var scope = app.Services.CreateScope())
{
    try { scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate(); }
    catch (Exception ex) { Log.Error(ex, "Migration error"); }
}

// 7. DEVELOPMENT-ИНСТРУМЕНТЫ
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 8. ОСНОВНЫЕ MIDDLEWARE
app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigin");
app.UseStaticFiles(); // ДО маршрутизации!
app.MapStaticAssets();

// 9. ДИАГНОСТИКА (только для API)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path == "/")
    {
        context.Response.Redirect("/calendar");
        return;
    }
    if (!string.IsNullOrEmpty(path) && path.Contains("api/"))
    {
        var hasAuthCookie = context.Request.Cookies.ContainsKey("AgroAdmin.Auth");
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var isMobile = userAgent.Contains("Android") || userAgent.Contains("iPhone") || userAgent.Contains("iPad");
        Log.Information("[AUTH DEBUG] {Path} | Cookie:{HasCookie} | Mobile:{IsMobile} | UA:{UA}",
            path, hasAuthCookie, isMobile, userAgent[..Math.Min(50, userAgent.Length)]);
    }
    await next();
});

// 10. АВТОРИЗАЦИЯ И ЭНДПОИНТЫ
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

// 11. BLAZOR И HEALTHCHECK
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AgroAdmin.Client._Imports).Assembly);
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();