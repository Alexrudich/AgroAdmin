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
        logger.LogInformation("🔍 DatabaseScannerJob started at {Time}", DateTime.UtcNow);

        var now = DateTime.UtcNow;
        logger.LogInformation("Current UTC time: {Now}", now);

        var pendingReminders = await dbContext.ScheduledReminders
            .Where(r => !r.IsSent && r.ScheduledFor <= now)
            .OrderByDescending(r => r.Priority)
            .Take(10)
            .ToListAsync();

        logger.LogInformation("Found {Count} pending reminders in DB", pendingReminders.Count);

        if (pendingReminders.Count == 0)
        {
            logger.LogInformation("No pending reminders found");
            return;
        }

        logger.LogInformation("🔍 Найдено {Count} напоминаний для отправки", pendingReminders.Count);

        foreach (var reminder in pendingReminders)
        {
            logger.LogInformation("Processing reminder {Id}: {Msg}, ScheduledFor: {Time}, TargetChatId: {ChatId}",
                reminder.Id, reminder.Message, reminder.ScheduledFor, reminder.TargetChatId);

            try
            {
                var prefix = reminder.Priority switch
                {
                    ReminderPriority.Urgent => "🚨 <b>СРОЧНО:</b> ",
                    ReminderPriority.High => "⚠️ <b>Внимание:</b> ",
                    ReminderPriority.Low => "ℹ️ ",
                    _ => "🔔 "
                };

                var fullMessage = prefix + reminder.Message;
                logger.LogInformation("Sending to Telegram: {Message}", fullMessage);

                // Отправляем сообщение. Если TargetChatId в базе null, 
                // TelegramService использует список из конфига.
                await telegram.SendMessageAsync(fullMessage, reminder.TargetChatId);

                reminder.IsSent = true;
                logger.LogInformation("✅ Отправлено: {Msg}", reminder.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Ошибка отправки напоминания #{Id}", reminder.Id);
            }
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("DatabaseScannerJob completed at {Time}", DateTime.UtcNow);
    }
}