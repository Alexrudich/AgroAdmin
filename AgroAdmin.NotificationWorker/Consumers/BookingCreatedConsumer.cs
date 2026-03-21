using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Domain.Models;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using MassTransit;
using AgroAdmin.Shared.Dto.Bookings.Events;

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
        var unitEmoji = msg.Unit switch
        {
            ReservedUnits.PondSide => "🌊",
            ReservedUnits.ParkingSide => "🚗",
            ReservedUnits.WholeHouse => "🏠",
            _ => "🏢"
        };

        // 2. Определяем время напоминания (за час до заезда)
        var checkInDateTime = msg.ArrivalDate.Date + msg.CheckInTime;
        if (msg.CheckInTime == TimeSpan.Zero)
        {
            checkInDateTime = msg.ArrivalDate.Date + new TimeSpan(14, 0, 0);
        }

        var reminderTime = checkInDateTime.AddHours(-1);
        if (reminderTime <= DateTime.UtcNow)
            reminderTime = DateTime.UtcNow.AddSeconds(10);

        // 3. Формируем текст сообщения (без лишних пустых строк)
        var checkInTimeStr = checkInDateTime.ToString("HH:mm");

        var text = $"🔔 <b>Скоро заезд!</b>\n" +
                   $"{unitEmoji} <b>{unitName}</b>\n" +
                   $"👤 <b>{msg.GuestName}</b>\n" +
                   $"📞 {msg.Phone}\n" +
                   $"📅 {msg.ArrivalDate:dd.MM.yyyy} — {msg.DepartureDate:dd.MM.yyyy}\n" +
                   $"⏰ Заезд: {checkInTimeStr}\n" +
                   $"👥 {msg.TotalGuestsCount} чел.";

        // Баня, если заказана
        if (msg.NeedsSauna)
        {
            text += $"\n🌡️ Баня заказана";
        }

        // Стоимость, если есть
        if (msg.AccommodationCost.HasValue && msg.AccommodationCost.Value > 0)
        {
            text += $"\n💰 Стоимость: {msg.AccommodationCost.Value:N0} ₽";
        }

        // Примечания, если есть
        if (!string.IsNullOrEmpty(msg.AdminNotes))
        {
            text += $"\n📝 Примечания: {msg.AdminNotes}";
        }

        // 4. Сохраняем в таблицу ScheduledReminders
        var reminder = new ScheduledReminder
        {
            Message = text,
            ScheduledFor = reminderTime,
            Priority = ReminderPriority.High,
            IsSent = false,
            TargetChatId = null
        };

        dbContext.ScheduledReminders.Add(reminder);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("📅 Авто-напоминание для брони #{BookingId} создано на {ReminderTime:dd.MM.yyyy HH:mm} (заезд в {CheckInTime})",
            msg.BookingId, reminderTime, checkInTimeStr);
    }
}