using AgroAdmin.Components;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.Shared.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/agroadmin-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddApplicationPart(typeof(AgroAdmin.API.Controllers.AuthController).Assembly);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:8080";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(frontendUrl) });

builder.Services.AddHttpContextAccessor();

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

builder.Services.AddHttpClient();
builder.Services.AddScoped<IBookingValidationService, BookingValidationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<ITelegramService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<TelegramService>>();
    return new TelegramService(httpClientFactory, configuration, logger);
});

builder.Services.AddScoped<BookingFormService>();

if (OperatingSystem.IsWindows())
{
    // На Windows Server/IIS ключи хранятся в реестре или профиле пользователя
    builder.Services.AddDataProtection()
        .SetApplicationName("AgroAdmin");
}
else
{
    // Для Linux/Docker
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .SetApplicationName("AgroAdmin");
}

var app = builder.Build();

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