using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Domain.Models;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using MassTransit;
using AgroAdmin.Shared.Dto.Bookings.Events;

namespace AgroAdmin.NotificationWorker.Consumers;

public class BookingCreatedConsumer(
    ILogger<BookingCreatedConsumer> logger,
    AppDbContext dbContext) : IConsumer<BookingCreatedEvent>, IConsumer<BookingUpdatedEvent>
{
    public async Task Consume(ConsumeContext<BookingCreatedEvent> context)
    {
        var msg = context.Message;
        await CreateReminderForBookingAsync(msg.BookingId, msg.GuestName, msg.Phone, msg.ArrivalDate, msg.DepartureDate, msg.Unit, msg.NeedsSauna, msg.AdminNotes, msg.CheckInTime, msg.AccommodationCost, msg.TotalGuestsCount);
    }

    public async Task Consume(ConsumeContext<BookingUpdatedEvent> context)
    {
        var msg = context.Message;
        await CreateReminderForBookingAsync(msg.BookingId, msg.GuestName, msg.Phone, msg.ArrivalDate, msg.DepartureDate, msg.Unit, msg.NeedsSauna, msg.AdminNotes, msg.CheckInTime, msg.AccommodationCost, msg.TotalGuestsCount);
    }

    private async Task CreateReminderForBookingAsync(
        int bookingId,
        string guestName,
        string phone,
        DateTime arrivalDate,
        DateTime departureDate,
        ReservedUnits unit,
        bool needsSauna,
        string? adminNotes,
        TimeSpan checkInTime,
        decimal? accommodationCost,
        int totalGuestsCount)
    {

        // 1. Превращаем Enum в красивый текст
        var unitName = unit.ToFriendlyString();
        var unitEmoji = unit switch
        {
            ReservedUnits.PondSide => "🌊",
            ReservedUnits.ParkingSide => "🚗",
            ReservedUnits.WholeHouse => "🏠",
            _ => "🏢"
        };

        // 2. Определяем время напоминания (за час до заезда по Минску)
        var minskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Belarus Standard Time");

        // Собираем локальную дату и время заезда (по Минску)
        var checkInLocal = arrivalDate.Date + checkInTime;
        if (checkInTime == TimeSpan.Zero)
        {
            checkInLocal = arrivalDate.Date + new TimeSpan(14, 0, 0);
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
                   $"👤 <b>{guestName}</b>\n" +
                   $"📞 {phone}\n" +
                   $"📅 {arrivalDate:dd.MM.yyyy} — {departureDate:dd.MM.yyyy}\n" +
                   $"⏰ Заезд: {checkInTimeStr}\n" +
                   $"👥 {totalGuestsCount} чел.";

        // Баня, если заказана
        if (needsSauna)
        {
            text += $"\n🌡️ Баня заказана";
        }

        // Стоимость, если есть
        if (accommodationCost.HasValue && accommodationCost.Value > 0)
        {
            text += $"\n💰 Стоимость: {accommodationCost.Value:N0} BYN";
        }

        // Примечания, если есть
        if (!string.IsNullOrEmpty(adminNotes))
        {
            text += $"\n📝 Примечания: {adminNotes}";
        }

        // 4. Сохраняем в таблицу ScheduledReminders
        var reminder = new ScheduledReminder
        {
            Message = text,
            ScheduledFor = reminderTimeUtc,
            Priority = ReminderPriority.High,
            IsSent = false,
            TargetChatId = null,
            BookingId = bookingId
        };

        dbContext.ScheduledReminders.Add(reminder);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("📅 Авто-напоминание для брони #{BookingId} создано на {ReminderTime:dd.MM.yyyy HH:mm} UTC (заезд в {CheckInTime} по Минску)",
            bookingId, reminderTimeUtc, checkInTimeStr);
    }
}