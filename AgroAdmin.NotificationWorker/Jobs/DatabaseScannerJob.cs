using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace AgroAdmin.NotificationWorker.Jobs;

[DisallowConcurrentExecution]
public class DatabaseScannerJob(
    AppDbContext dbContext,
    ITelegramService telegram,
    ILogger<DatabaseScannerJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.UtcNow;

        var pendingReminders = await dbContext.ScheduledReminders
            .Where(r => !r.IsSent && r.ScheduledFor <= now)
            .OrderByDescending(r => r.Priority)
            .Take(10)
            .ToListAsync();

        if (pendingReminders.Count == 0) return;

        logger.LogInformation("🔍 Найдено {Count} напоминаний для отправки", pendingReminders.Count);

        foreach (var reminder in pendingReminders)
        {
            try
            {
                var prefix = reminder.Priority switch
                {
                    ReminderPriority.Urgent => "🚨 <b>СРОЧНО:</b> ",
                    ReminderPriority.High => "⚠️ <b>Внимание:</b> ",
                    ReminderPriority.Low => "ℹ️ ",
                    _ => "🔔 "
                };

                // Отправляем сообщение. Если TargetChatId в базе null, 
                // TelegramService использует список из конфига.
                await telegram.SendMessageAsync(prefix + reminder.Message, reminder.TargetChatId);

                reminder.IsSent = true;
                logger.LogInformation("✅ Отправлено: {Msg}", reminder.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Ошибка отправки напоминания #{Id}", reminder.Id);
            }
        }

        await dbContext.SaveChangesAsync();
    }
}