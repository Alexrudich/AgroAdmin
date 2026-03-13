using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.Infrastructure.BackgroundServices;

public class DatabaseScannerService(
    IServiceProvider services,
    ILogger<DatabaseScannerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Database scanner started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

                var pendingReminders = await notificationService.GetActiveRemindersAsync();
                var now = DateTime.UtcNow;

                foreach (var reminder in pendingReminders.Where(r => r.ScheduledFor <= now))
                {
                    logger.LogInformation("Sending reminder {Id}", reminder.Id);

                    var prefix = reminder.Priority switch
                    {
                        ReminderPriority.Urgent => "🚨 <b>СРОЧНО:</b> ",
                        ReminderPriority.High => "⚠️ <b>Внимание:</b> ",
                        ReminderPriority.Low => "ℹ️ ",
                        _ => "🔔 "
                    };

                    await telegram.SendMessageAsync(prefix + reminder.Message, reminder.TargetChatId);

                    // Отметить как отправленное (нужно добавить метод в INotificationService)
                    await notificationService.MarkAsSentAsync(reminder.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in database scanner");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}