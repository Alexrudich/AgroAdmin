using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace AgroAdmin.NotificationWorker.Jobs;

[DisallowConcurrentExecution] // Чтобы два сканера не слали одно и то же одновременно
public class DatabaseScannerJob(
    AppDbContext dbContext,
    ITelegramService telegram,
    ILogger<DatabaseScannerJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.UtcNow;

        // 1. Ищем задачи, время которых пришло, но они не отправлены
        var pendingReminders = await dbContext.ScheduledReminders
            .Where(r => !r.IsSent && r.ScheduledFor <= now)
            .OrderBy(r => r.Priority) // Сначала срочные
            .Take(10) // Берем пачкой, чтобы не забить лимиты Телеграма
            .ToListAsync();

        if (pendingReminders.Count == 0) return;

        logger.LogInformation("🔍 Найдено {Count} напоминаний для отправки", pendingReminders.Count);

        foreach (var reminder in pendingReminders)
        {
            try
            {
                // Если TargetChatId пустой — TelegramService сам возьмет дефолтные из конфига
                await telegram.SendMessageAsync(reminder.Message);

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