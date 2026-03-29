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

        // 2. Определяем время напоминания (за час до заезда по Минску)
        var minskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Belarus Standard Time");

        // Собираем локальную дату и время заезда (по Минску)
        var checkInLocal = msg.ArrivalDate.Date + msg.CheckInTime;
        if (msg.CheckInTime == TimeSpan.Zero)
        {
            checkInLocal = msg.ArrivalDate.Date + new TimeSpan(14, 0, 0);
        }

        // Явно указываем, что это локальное время Минска (Kind = Unspecified)
        var checkInUnspecified = DateTime.SpecifyKind(checkInLocal, DateTimeKind.Unspecified);

        // Конвертируем в UTC, указывая исходную временную зону
        var checkInUtc = TimeZoneInfo.ConvertTimeToUtc(checkInUnspecified, minskTimeZone);

        // Вычитаем 1 час для напоминания
        var reminderTimeUtc = checkInUtc.AddHours(-1);

        // Если напоминание уже в прошлом - отправляем через 10 секунд
        if (reminderTimeUtc <= DateTime.UtcNow)
            reminderTimeUtc = DateTime.UtcNow.AddSeconds(10);

        // 3. Формируем текст сообщения
        var checkInTimeStr = checkInLocal.ToString("HH:mm");

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
            text += $"\n💰 Стоимость: {msg.AccommodationCost.Value:N0} BYN";
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
            ScheduledFor = reminderTimeUtc,
            Priority = ReminderPriority.High,
            IsSent = false,
            TargetChatId = null
        };

        dbContext.ScheduledReminders.Add(reminder);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("📅 Авто-напоминание для брони #{BookingId} создано на {ReminderTime:dd.MM.yyyy HH:mm} UTC (заезд в {CheckInTime} по Минску)",
            msg.BookingId, reminderTimeUtc, checkInTimeStr);
    }
}