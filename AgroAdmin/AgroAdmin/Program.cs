using AgroAdmin.API.Controllers;
using AgroAdmin.Components;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AgroAdmin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/agroadmin-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Starting AgroAdmin application");

                var builder = WebApplication.CreateBuilder(args);

                // 👇 ПОДКЛЮЧАЕМ SERILOG
                builder.Host.UseSerilog();

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveWebAssemblyComponents();
                builder.Services.AddControllers()
                    .AddApplicationPart(typeof(BookingsController).Assembly);
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

                var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:8080";
                builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(frontendUrl) });

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

                builder.Services.AddHttpContextAccessor();
                builder.Services.AddScoped<IAuthService, AuthService>();

                // 👇 АВТОРИЗАЦИЯ (минимально)
                builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options =>
                    {
                        options.LoginPath = "/login";
                        options.Cookie.Name = ".AgroAdmin.Auth";
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    });
                builder.Services.AddAuthorization();

                // 👇 ТЕЛЕГРАМ
                builder.Services.AddHttpClient();
                builder.Services.AddSingleton<ITelegramService>(sp =>
                {
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var logger = sp.GetRequiredService<ILogger<TelegramService>>();
                    return new TelegramService(httpClientFactory, configuration, logger);
                });

                var app = builder.Build();

                // Миграции
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

                // Pipeline
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
                app.UseCors("AllowSpecificOrigin"); // 👈 ИСПРАВЛЕНО
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseAntiforgery();

                app.MapControllers();
                app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/")
                    {
                        context.Response.Redirect("/calendar");
                        return;
                    }
                    await next();
                });

                app.MapStaticAssets();
                app.MapRazorComponents<App>()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(typeof(AgroAdmin.Client._Imports).Assembly);

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}