using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Domain.Models;
using AgroAdmin.Shared.Dto.Bookings;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using MassTransit;

namespace AgroAdmin.NotificationWorker.Consumers;

public class BookingCreatedConsumer(
    ILogger<BookingCreatedConsumer> logger,
    AppDbContext dbContext) : IConsumer<BookingCreatedEvent>
{
    public async Task Consume(ConsumeContext<BookingCreatedEvent> context)
    {
        var msg = context.Message;

        // 1. Превращаем Enum в красивый текст
        var unitName = msg.Unit.ToFriendlyString();

        // 2. Расчет времени (за час до заезда)
        var reminderTime = msg.ArrivalDate.AddHours(-1);
        if (reminderTime <= DateTime.UtcNow)
            reminderTime = DateTime.UtcNow.AddSeconds(10);

        // 3. Формируем текст сообщения для базы
        var text = $"""
                    🔔 <b>Скоро заезд!</b>
                    🏠 Объект: <b>{unitName}</b>
                    👤 Гость: <b>{msg.GuestName}</b>
                    📞 Тел: {msg.Phone}
                    ⏰ Время заезда: {msg.ArrivalDate:HH:mm}
                    """;

        // 4. Сохраняем в таблицу ScheduledReminders
        var reminder = new ScheduledReminder
        {
            Message = text,
            ScheduledFor = reminderTime,
            Priority = ReminderPriority.High,
            IsSent = false
        };

        dbContext.ScheduledReminders.Add(reminder);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("📅 Авто-напоминание для #{Id} создано на {Time}", msg.BookingId, reminderTime);
    }
}