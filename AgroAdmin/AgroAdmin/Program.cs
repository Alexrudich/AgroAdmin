using AgroAdmin.Components;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.BackgroundServices;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.Infrastructure.Services.Pricing;
using AgroAdmin.Infrastructure.Services.Pricing.Strategies;
using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.Shared.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ЛОГИРОВАНИЕ ---
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// --- 2. БАЗА И КОНТРОЛЛЕРЫ ---
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AgroAdmin.API.Controllers.AuthController).Assembly);
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// --- 3. БЕЗОПАСНОСТЬ ---
builder.Services.AddCors(options => {
    options.AddPolicy("AllowSpecificOrigin", policy => {
        policy.WithOrigins("https://agroadmin.runasp.net", "http://localhost:8080")
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "AgroAdmin.Auth";
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Всегда Secure
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // Умный выбор SameSite в зависимости от браузера
        options.Cookie.SameSite = SameSiteMode.Lax; // Значение по умолчанию

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
            },
            // перед тем как кука будет создана, проверяем User-Agent
            OnSigningIn = ctx =>
            {
                var userAgent = ctx.Request.Headers["User-Agent"].ToString();

                // Определяем старые Safari (iOS 12 и ниже) и некоторые версии Chrome
                var isProblemBrowser = userAgent.Contains("iPhone OS 12") ||
                                       userAgent.Contains("iPad; CPU OS 12") ||
                                       userAgent.Contains("CFNetwork"); // для старых

                // Для проблемных браузеров — не указываем SameSite вообще
                ctx.CookieOptions.SameSite = isProblemBrowser ? SameSiteMode.Unspecified :
                    // Для всех остальных — явно None (как мы хотим)
                    SameSiteMode.None;

                return Task.CompletedTask;
            }
        };
    }); 
builder.Services.AddAuthorization();

// --- 3.1 DATA PROTECTION ---
try
{
    var keysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys");
    Directory.CreateDirectory(keysPath);

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("AgroAdmin");

    Log.Information("API Data Protection keys directory: {KeysPath}", keysPath);
}
catch (Exception ex)
{
    Log.Warning(ex, "API failed to setup Data Protection keys directory, using ephemeral keys");
    builder.Services.AddDataProtection()
        .SetApplicationName("AgroAdmin");
}

// --- 4. СЕРВИСЫ ---
builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddSingleton<ITelegramApiClient, TelegramApiClient>();
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddSingleton<DatabaseScannerService>();
builder.Services.AddSingleton<IDatabaseBackupService>(sp => sp.GetRequiredService<DatabaseBackupService>());

builder.Services.AddHostedService<DatabaseBackupService>();
builder.Services.AddHostedService<DatabaseScannerService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingValidationService, BookingValidationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<BookingFormService>();
builder.Services.AddScoped<IPricingStrategy, BasePriceStrategy>();
builder.Services.AddScoped<IPricingStrategy, AdditionalServicesStrategy>();
builder.Services.AddScoped<PricingEngine>();
builder.Services.AddScoped<PricingConfigurationService>();
builder.Services.AddScoped<IBookingTelegramService, BookingTelegramService>();
builder.Services.AddScoped<IHealthService, HealthService>();

// --- 5. MASSTRANSIT ---
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

// --- МИГРАЦИИ ---
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        var configService = scope.ServiceProvider.GetRequiredService<PricingConfigurationService>();
        await configService.EnsureDefaultConfigurationAsync();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Migration or seeding error");
    }
}

// --- ПАЙПЛАЙН ---
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

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigin");
app.UseStaticFiles(); // Статика до middleware

// --- ЗАПУСК TELEGRAM БОТА ---
try
{
    var telegramService = app.Services.GetRequiredService<ITelegramService>();
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

    // Запускаем получение команд
    _ = telegramService.StartReceivingAsync(lifetime.ApplicationStopping);

    // Регистрируем остановку при завершении приложения
    lifetime.ApplicationStopping.Register(() =>
    {
        telegramService.StopReceivingAsync().Wait();
    });

    Log.Information("Telegram bot started successfully");
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to start Telegram bot");
}

// --- ДИАГНОСТИКА АВТОРИЗАЦИИ (только для API, пропускаем статику) ---
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;

    // Редирект с корня
    if (path == "/")
    {
        context.Response.Redirect("/calendar");
        return;
    }

    // Пропускаем статические файлы без логирования
    if (path != null && (path.StartsWith("/_framework") ||
                         path.StartsWith("/css") ||
                         path.StartsWith("/js") ||
                         path.Contains($".")))
    {
        await next();
        return;
    }

    // Логируем только API-запросы
    if (!string.IsNullOrEmpty(path) && path.Contains("api/"))
    {
        var hasAuthCookie = context.Request.Cookies.ContainsKey("AgroAdmin.Auth");
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var isMobile = userAgent.Contains("Android") || userAgent.Contains("iPhone") || userAgent.Contains("iPad");

        Log.Information("[AUTH DEBUG] Path: {Path}, HasCookie: {HasCookie}, IsMobile: {IsMobile}, UA: {UserAgent}",
            path, hasAuthCookie, isMobile, userAgent[..Math.Min(50, userAgent.Length)]);
    }

    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AgroAdmin.Client._Imports).Assembly);

app.MapMethods("/health", ["GET", "HEAD"], () => Results.Ok("Healthy"));

app.Run();