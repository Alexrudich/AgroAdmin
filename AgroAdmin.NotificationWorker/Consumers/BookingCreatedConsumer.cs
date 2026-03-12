using AgroAdmin.NotificationWorker.Jobs;
using AgroAdmin.Shared.Dto.Bookings;
using MassTransit;
using Quartz;

namespace AgroAdmin.NotificationWorker.Consumers;

public class BookingCreatedConsumer(ILogger<BookingCreatedConsumer> logger, ISchedulerFactory schedulerFactory) : IConsumer<BookingCreatedEvent>
{
    public async Task Consume(ConsumeContext<BookingCreatedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation("🎯 [КРОЛИК] Поймали бронь #{Id}. Планируем напоминание...", msg.BookingId);

        var scheduler = await schedulerFactory.GetScheduler();

        // 1. Явно создаем словарь данных (Map)
        var jobData = new JobDataMap();
        jobData.Add("BookingId", msg.BookingId);
        jobData.Add("GuestName", msg.GuestName);

        // 2. Создаем задачу и привязываем данные
        var job = JobBuilder.Create<ReminderJob>()
            .WithIdentity($"ReminderJob_{msg.BookingId}", "BookingGroup")
            .UsingJobData(jobData) // Передаем всю карту данных
            .Build();

        // 3. Создаем триггер на +10 секунд от текущего момента
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"Trigger_{msg.BookingId}", "BookingGroup")
            .StartAt(DateTimeOffset.Now.AddSeconds(10))
            .Build();

        // 4. Ставим в расписание
        await scheduler.ScheduleJob(job, trigger);

        logger.LogInformation("⏳ [QUARTZ] Задача для {Name} поставлена в очередь на +10 сек", msg.GuestName);
    }
}