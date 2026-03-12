using AgroAdmin.Infrastructure.Abstractions;
using Quartz;

namespace AgroAdmin.NotificationWorker.Jobs;

public class ReminderJob(ILogger<ReminderJob> logger, ITelegramService telegram) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var guestName = context.MergedJobDataMap.GetString("GuestName");
        var bookingId = context.MergedJobDataMap.GetIntValue("BookingId");

        var message = $"⏰ <b>Напоминание!</b>\nПора встречать гостя: <b>{guestName}</b> (Бронь #{bookingId})";

        // Используем твой оригинальный метод
        await telegram.SendMessageAsync(message);

        logger.LogInformation("🚀 [SUCCESS] Напоминание отправлено через ITelegramService");
    }
}