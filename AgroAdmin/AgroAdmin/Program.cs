using AgroAdmin.Components;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.NotificationWorker.Jobs;
using AgroAdmin.Shared.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ЛОГИРОВАНИЕ (Serilog) ---
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/agroadmin-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// --- 2. БАЗА ДАННЫХ И СТАНДАРТНЫЕ СЕРВИСЫ ASP.NET ---
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AgroAdmin.API.Controllers.AuthController).Assembly);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// --- 3. БЕЗОПАСНОСТЬ (CORS, Auth, DataProtection) ---
var allowedOrigins = new[] { "https://agroadmin.runasp.net" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "AgroAdmin.Auth";
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddDataProtection().SetApplicationName("AgroAdmin");
}
else
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .SetApplicationName("AgroAdmin");
}

// --- 4. СЕРВИСЫ ПРИЛОЖЕНИЯ (Infrastructure & Shared) ---
// Scoped сервисы
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingValidationService, BookingValidationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<BookingFormService>();

// Singleton сервисы 
builder.Services.AddSingleton<ITelegramService, TelegramService>();

// Настройка HttpClient для фронтенда
var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:8080";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(frontendUrl) });

// --- 5. ФОНОВЫЕ ЗАДАЧИ (Quartz.NET) ---
builder.Services.AddQuartz(q =>
{
    q.AddJob<ReminderJob>(opts => opts
        .WithIdentity("ReminderJob")
        .StoreDurably());
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// --- 6. ШИНА ДАННЫХ (MassTransit & RabbitMQ) ---
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitUrl = builder.Configuration["RabbitMQ:Url"]
                        ?? builder.Configuration["RabbitMQ__Url"]
                        ?? Environment.GetEnvironmentVariable("RabbitMQ__Url");

        if (!string.IsNullOrEmpty(rabbitUrl))
        {
            var cleanUrl = rabbitUrl.Trim().TrimEnd('/');
            try
            {
                cfg.Host(new Uri(cleanUrl));
                cfg.ConfigureEndpoints(context);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RabbitMQ URI error: {Url}", cleanUrl);
            }
        }
    });
});

// --- 7. КОНФИГУРАЦИЯ PIPELINE (Middleware) ---
var app = builder.Build();

// Автоматические миграции БД при старте
using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Migration applying error");
    }
}

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
app.UseStaticFiles();
app.MapStaticAssets();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();

// Редирект с корня на календарь
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/calendar");
        return;
    }
    await next();
});

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AgroAdmin.Client._Imports).Assembly);

app.Run();
