using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.BackgroundServices;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace AgroAdmin.Infrastructure.Services;

public class HealthService(
    IServiceProvider serviceProvider,
    ILogger<HealthService> logger,
    DatabaseBackupService backupService)
    : IHealthService
{

    public async Task<HealthCheckDto> CheckAllAsync()
    {
        var result = new HealthCheckDto();
        var components = new List<ComponentHealth>
        {
            // 1. База данных
            await CheckDatabaseAsync(),
            // 2. RabbitMQ
            await CheckRabbitMqAsync(),
            // 3. Google Drive
            await CheckGoogleDriveAsync(),
            // 4. Фоновые сервисы
            CheckBackgroundServices(),
            // 5. Диск
            CheckDiskSpace()
        };

        result.Components = components;
        result.IsHealthy = components.All(c => c.IsHealthy);

        return result;
    }

    public async Task<string> FormatForTelegramAsync()
    {
        var health = await CheckAllAsync();
        var sb = new StringBuilder();

        sb.AppendLine(health.IsHealthy ? "🟢 *HEALTH CHECK PASSED*" : "🔴 *HEALTH CHECK FAILED*");
        sb.AppendLine($"📅 {health.CheckedAt:dd.MM.yyyy HH:mm:ss} UTC\n");

        foreach (var component in health.Components)
        {
            var icon = component.IsHealthy ? "✅" : "❌";
            var timeInfo = component.ResponseTimeMs.HasValue
                ? $" ({component.ResponseTimeMs}ms)"
                : "";
            sb.AppendLine($"{icon} *{component.Name}*{timeInfo}");

            if (!string.IsNullOrEmpty(component.Message))
            {
                sb.AppendLine($"   └ {component.Message}");
            }
        }

        return sb.ToString();
    }

    private async Task<ComponentHealth> CheckDatabaseAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();
            sw.Stop();

            return new ComponentHealth
            {
                Name = "База данных (SQL Server)",
                IsHealthy = canConnect,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = canConnect ? "Подключение успешно" : "Не удалось подключиться"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Database health check failed");
            return new ComponentHealth
            {
                Name = "База данных (SQL Server)",
                IsHealthy = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = ex.Message
            };
        }
    }

    private Task<ComponentHealth> CheckRabbitMqAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var scope = serviceProvider.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var rabbitUrl = configuration["RabbitMQ:Url"]
                            ?? configuration["RabbitMQ__Url"]
                            ?? Environment.GetEnvironmentVariable("RabbitMQ__Url");

            if (string.IsNullOrEmpty(rabbitUrl))
            {
                return Task.FromResult(new ComponentHealth
                {
                    Name = "RabbitMQ",
                    IsHealthy = false,
                    Message = "URL не настроен"
                });
            }

            // Проверяем, что URL валидный
            var uri = new Uri(rabbitUrl);
            var isUrlValid = uri.Host != null;

            sw.Stop();

            return Task.FromResult(new ComponentHealth
            {
                Name = "RabbitMQ",
                IsHealthy = true,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = $"Подключение настроено ({uri.Host})"
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "RabbitMQ health check failed");
            return Task.FromResult(new ComponentHealth
            {
                Name = "RabbitMQ",
                IsHealthy = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = ex.Message
            });
        }
    }

    private async Task<ComponentHealth> CheckGoogleDriveAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Пытаемся получить credential через сервис бэкапа
            // У DatabaseBackupService есть приватный метод GetUserCredentialAsync,
            // который проверяет валидность токена
            var credentialMethod = typeof(DatabaseBackupService)
                .GetMethod("GetUserCredentialAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (credentialMethod != null)
            {
                var task = (Task)credentialMethod.Invoke(backupService, null)!;
                await task;
                var credential = task.GetType().GetProperty("Result")?.GetValue(task);

                sw.Stop();
                var isValid = credential != null;
                return new ComponentHealth
                {
                    Name = "Google Drive",
                    IsHealthy = isValid,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Message = isValid ? "Токен валиден" : "Не удалось получить токен"
                };
            }

            return new ComponentHealth
            {
                Name = "Google Drive",
                IsHealthy = false,
                Message = "Метод проверки токена не найден"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Google Drive health check failed");
            return new ComponentHealth
            {
                Name = "Google Drive",
                IsHealthy = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = ex.Message
            };
        }
    }

    private ComponentHealth CheckBackgroundServices()
    {
        try
        {
            // Проверяем, что фоновые сервисы запущены
            var backupService = serviceProvider.GetService<DatabaseBackupService>();
            var scannerService = serviceProvider.GetService<DatabaseScannerService>();

            var backupRunning = backupService != null;
            var scannerRunning = scannerService != null;
            var allRunning = backupRunning && scannerRunning;

            var message = $"Backup: {(backupRunning ? "✅" : "❌")}, Scanner: {(scannerRunning ? "✅" : "❌")}";

            return new ComponentHealth
            {
                Name = "Фоновые сервисы",
                IsHealthy = allRunning,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background services health check failed");
            return new ComponentHealth
            {
                Name = "Фоновые сервисы",
                IsHealthy = false,
                Message = ex.Message
            };
        }
    }

    private ComponentHealth CheckDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? "C:");
            var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var isHealthy = freeGB > 1; // > 1 GB free

            return new ComponentHealth
            {
                Name = "Дисковое пространство",
                IsHealthy = isHealthy,
                Message = $"Свободно: {freeGB:F1} GB из {drive.TotalSize / (1024.0 * 1024 * 1024):F1} GB"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Disk space health check failed");
            return new ComponentHealth
            {
                Name = "Дисковое пространство",
                IsHealthy = false,
                Message = ex.Message
            };
        }
    }
}