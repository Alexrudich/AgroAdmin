using System.Text;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Dto.Telegram.Responses;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;

namespace AgroAdmin.Shared.Services
{
    public static class TelegramMessageFormatter
    {
        public static string FormatNearestBookings(List<TelegramBookingDto> bookings, int days)
        {
            if (!bookings.Any())
            {
                return $"📭 *Нет бронирований на ближайшие {days} дней.*\n\n" +
                       "Используйте /checkFreeSlots чтобы посмотреть свободные даты.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📅 *Ближайшие бронирования ({days} дней):*\n");

            foreach (var booking in bookings.Take(10))
            {
                var nights = (booking.DepartureDate - booking.ArrivalDate).Days;
                var nightsText = nights switch
                {
                    1 => "ночь",
                    <= 4 => "ночи",
                    _ => "ночей"
                };

                sb.AppendLine($"*{booking.UnitEmoji} {booking.GuestName}*");
                sb.AppendLine($"   📅 {booking.ArrivalDate:dd.MM} — {booking.DepartureDate:dd.MM} ({nights} {nightsText})");
                sb.AppendLine($"   👥 {booking.TotalGuestsCount} чел." + (booking.NeedsSauna ? " 🌡️" : ""));
                sb.AppendLine($"   🏠 {booking.UnitName}");

                if (booking.TotalPrice.HasValue && booking.TotalPrice.Value > 0)
                    sb.AppendLine($"   💰 {booking.TotalPrice.Value:N0} BYN");

                if (!string.IsNullOrEmpty(booking.Phone))
                    sb.AppendLine($"   📞 {booking.Phone}");

                sb.AppendLine();
            }

            sb.AppendLine($"📊 *Итого:* {bookings.Count} бронирований");
            return sb.ToString();
        }

        public static string FormatAvailabilitySummary(List<DailyAvailability> availability, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"🏠 *СВОДКА ЗАГРУЗКИ*");
            sb.AppendLine($"📅 {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}\n");

            var fullyFreeDates = availability.Where(d => d.IsFullyFree).Select(d => d.Date).ToList();
            sb.AppendLine(FormatDateRanges("🟢 *Полностью свободно* (все 3 объекта)", fullyFreeDates));

            var partiallyFree = availability.Where(d => d.IsPartiallyFree).ToList();
            if (partiallyFree.Any())
            {
                sb.AppendLine($"\n🟡 *Частично занято* (свободен 1-2 объекта):");

                var onlyPondFree = partiallyFree.Where(d => d.IsPondSideFree && !d.IsParkingSideFree).Select(d => d.Date).ToList();
                var onlyParkingFree = partiallyFree.Where(d => !d.IsPondSideFree && d.IsParkingSideFree).Select(d => d.Date).ToList();
                var bothFreeButWholeOccupied = partiallyFree.Where(d => d.IsPondSideFree && d.IsParkingSideFree && !d.IsWholeHouseFree).Select(d => d.Date).ToList();

                if (onlyPondFree.Any())
                    sb.AppendLine(FormatDateRanges($"   🌊 Только {ReservedUnits.PondSide.ToFriendlyString()}", onlyPondFree));

                if (onlyParkingFree.Any())
                    sb.AppendLine(FormatDateRanges($"   🚗 Только {ReservedUnits.ParkingSide.ToFriendlyString()}", onlyParkingFree));

                if (bothFreeButWholeOccupied.Any())
                    sb.AppendLine(FormatDateRanges($"   🏠 Занят {ReservedUnits.WholeHouse.ToFriendlyString()}, свободны обе половинки", bothFreeButWholeOccupied));
            }

            var fullyOccupied = availability.Where(d => d.IsFullyOccupied).Select(d => d.Date).ToList();
            if (fullyOccupied.Any())
            {
                sb.AppendLine($"\n🔴 *Полностью занято* (все 3 объекта):");
                sb.AppendLine(FormatDateRanges("   ", fullyOccupied));
            }

            var totalDays = availability.Count;
            var freeDays = fullyFreeDates.Count;
            var freePercent = totalDays > 0 ? (freeDays * 100.0 / totalDays) : 0;

            sb.AppendLine($"\n📊 *Статистика:*");
            sb.AppendLine($"   • Всего дней: {totalDays}");
            sb.AppendLine($"   • Полностью свободных: {freeDays} ({freePercent:F0}%)");
            sb.AppendLine($"   • Частично занятых: {partiallyFree.Count}");
            sb.AppendLine($"   • Полностью занятых: {fullyOccupied.Count}");

            return sb.ToString();
        }

        public static string FormatBookingMessage(BookingDto booking)
        {
            var unitName = booking.ReservedUnit.ToFriendlyString();
            var unitEmoji = booking.ReservedUnit switch
            {
                ReservedUnits.PondSide => "🌊",
                ReservedUnits.ParkingSide => "🚗",
                ReservedUnits.WholeHouse => "🏠",
                _ => "🏢"
            };

            var checkInTime = booking.CheckInTime != TimeSpan.Zero
                ? booking.CheckInTime.ToString(@"hh\:mm")
                : "14:00";

            var costLine = booking.AccommodationCost.HasValue && booking.AccommodationCost.Value > 0
                ? $"\n💰 Стоимость: {booking.AccommodationCost.Value:N0} BYN"
                : string.Empty;

            return $"""
                    <b>Новое бронирование!</b>

                    👤 Гость: {booking.Guest?.FullName}
                    📞 Телефон: {booking.Guest?.Phone}
                    📅 Даты: {booking.ArrivalDate:dd.MM.yyyy} — {booking.DepartureDate:dd.MM.yyyy}
                    ⏰ Заезд: {checkInTime}
                    🏠 Объект: {unitEmoji} {unitName}
                    👥 Гостей: {booking.TotalGuestsCount} (взр: {booking.AdultsCount}, дети: {booking.ChildrenCount}, мл: {booking.InfantsCount})
                    🐕 Собака: {(booking.HasDog ? "✅" : "❌")}
                    🌡️ Баня: {(booking.NeedsSauna ? "✅" : "❌")}
                    🥂 Зал: {(booking.NeedsBanquetHall ? "✅" : "❌")}
                    {costLine}
                    """;
        }

        public static string FormatBookingSummary(BookingSummaryDto summary)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"📊 *СВОДКА ПО БРОНИРОВАНИЯМ*");
            sb.AppendLine($"📅 {summary.StartDate:dd.MM.yyyy} — {summary.EndDate:dd.MM.yyyy}\n");

            sb.AppendLine($"🏠 *Бронирования:* {summary.TotalBookings}");
            sb.AppendLine($"👥 *Гости:* {summary.TotalGuests} чел.");
            sb.AppendLine($"💰 *Выручка:* {summary.TotalRevenue:N0} BYN");
            sb.AppendLine($"📈 *Загрузка:* {summary.OccupancyRate:F1}%");

            var nightsText = summary.AvgStayLength switch
            {
                < 1.1 => "ночь",
                < 2.1 => "ночи",
                _ => "ночей"
            };
            sb.AppendLine($"⭐ *Средняя продолжительность:* {summary.AvgStayLength:F1} {nightsText}");

            if (summary.CancelledBookings > 0)
            {
                sb.AppendLine($"🔄 *Отменено:* {summary.CancelledBookings}");
            }
            sb.AppendLine($"✅ *Завершено:* {summary.CompletedBookings}");

            return sb.ToString();
        }

        private static string FormatDateRanges(string title, List<DateTime> dates)
        {
            if (!dates.Any()) return "";

            var ranges = new List<string>();
            var sorted = dates.OrderBy(d => d).ToList();

            DateTime? rangeStart = null;
            DateTime? prevDate = null;

            foreach (var date in sorted)
            {
                if (rangeStart == null)
                {
                    rangeStart = date;
                }
                else if (prevDate.HasValue && (date - prevDate.Value).Days > 1)
                {
                    // Разрыв — закрываем текущий диапазон
                    ranges.Add(FormatSingleRange(rangeStart.Value, prevDate.Value));
                    rangeStart = date;
                }

                prevDate = date;
            }

            // Добавляем последний диапазон
            if (rangeStart.HasValue && prevDate.HasValue)
            {
                ranges.Add(FormatSingleRange(rangeStart.Value, prevDate.Value));
            }

            if (string.IsNullOrEmpty(title))
                return string.Join(", ", ranges);

            return $"{title}: {string.Join(", ", ranges)}";
        }

        private static string FormatSingleRange(DateTime start, DateTime end)
        {
            if (start == end)
                return $"{start:dd.MM}";

            return $"{start:dd.MM}—{end:dd.MM}";
        }
    }
}
