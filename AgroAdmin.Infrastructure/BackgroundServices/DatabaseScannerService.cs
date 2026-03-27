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
        logger.LogInformation("✅ Database scanner started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

                var pendingReminders = await notificationService.GetActiveRemindersAsync();
                var now = DateTime.UtcNow;

                var remindersToSend = pendingReminders.Where(r => r.ScheduledFor <= now).ToList();

                if (remindersToSend.Any())
                {
                    logger.LogInformation("📬 Sending {Count} reminders", remindersToSend.Count);

                    foreach (var reminder in remindersToSend)
                    {
                        var prefix = reminder.Priority switch
                        {
                            ReminderPriority.Urgent => "🚨 <b>СРОЧНО:</b> ",
                            ReminderPriority.High => "⚠️ <b>Внимание:</b> ",
                            ReminderPriority.Low => "ℹ️ ",
                            _ => "🔔 "
                        };

                        await telegram.SendMessageAsync(prefix + reminder.Message, reminder.TargetChatId);
                        await notificationService.MarkAsSentAsync(reminder.Id);
                    }
                }
                else
                {
                    // Логируем только в Debug режиме или раз в минуту
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("No reminders to send");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error in database scanner");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        logger.LogInformation("🛑 Database scanner stopped");
    }
}