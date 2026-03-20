using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Bookings.Requests;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.Infrastructure.Services;

public class BookingValidationService(AppDbContext context, ILogger<BookingValidationService> logger)
    : IBookingValidationService
{
    private record UnitCapacity(int MaxAdults, int MaxInfants, int TotalBeds, bool AllowsExtraMattress);

    private UnitCapacity GetCapacityForUnit(ReservedUnits unit) => unit switch
    {
        ReservedUnits.PondSide => new(7, 1, 7, true),      // 3 пары + 1 одиночка + 1 младенец
        ReservedUnits.ParkingSide => new(7, 0, 7, true),   // 3 пары + 1 одиночка
        ReservedUnits.WholeHouse => new(13, 1, 14, true),  // 6 пар + 1 одиночка + 1 младенец + матрас
        _ => throw new ArgumentException($"Unknown unit: {unit}")
    };

    public BookingValidationResult ValidateGuests(CreateBookingDto booking)
    {
        try
        {
            logger.LogInformation("Validating guests for booking: {Unit}", booking.ReservedUnit);

            var result = new BookingValidationResult { IsValid = true };
            var capacity = GetCapacityForUnit(booking.ReservedUnit);

            var totalGuests = booking.AdultsCount + booking.ChildrenCount;

            if (totalGuests > capacity.TotalBeds)
            {
                result.IsValid = false;
                result.Errors.Add($"Максимальное количество гостей: {capacity.TotalBeds} (у вас {totalGuests})");

                if (capacity.AllowsExtraMattress && totalGuests <= capacity.TotalBeds + 1)
                {
                    result.Errors.Add("Можно добавить надувной матрас");
                }
            }

            if (booking.InfantsCount > capacity.MaxInfants)
            {
                result.IsValid = false;
                result.Errors.Add($"Максимальное количество младенцев: {capacity.MaxInfants}");
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating guests");
            return new BookingValidationResult
            {
                IsValid = false,
                Errors = { "Ошибка валидации" }
            };
        }
    }

    public async Task<BookingValidationResult> ValidateDatesAsync(CreateBookingDto booking, int? currentBookingId = null)
    {
        try
        {
            logger.LogInformation("Validating dates for booking: {Unit} from {Arrival} to {Departure}",
                booking.ReservedUnit, booking.ArrivalDate, booking.DepartureDate);

            var result = new BookingValidationResult { IsValid = true };

            // 1. Проверка доступности дат по юнитам
            await ValidateUnitAvailabilityAsync(booking, currentBookingId, result);

            // 2. Проверка сауны и банкетного зала
            if (booking.NeedsSauna || booking.NeedsBanquetHall)
            {
                await ValidateOptionsConflictsAsync(booking, currentBookingId, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating dates");
            return new BookingValidationResult
            {
                IsValid = false,
                Errors = { "Ошибка проверки доступности дат" }
            };
        }
    }

    private async Task ValidateUnitAvailabilityAsync(CreateBookingDto booking, int? currentBookingId, BookingValidationResult result)
    {
        var currentDate = booking.ArrivalDate;
        DateTime? firstBusyDate = null;

        while (currentDate < booking.DepartureDate)
        {
            var isAvailable = await CheckDateAvailabilityAsync(currentDate, booking.ReservedUnit, currentBookingId);

            if (!isAvailable)
            {
                firstBusyDate ??= currentDate;
            }

            currentDate = currentDate.AddDays(1);
        }

        if (firstBusyDate.HasValue)
        {
            result.IsValid = false;
            result.EarliestAvailableDate = firstBusyDate;
            result.Errors.Add($"Объект занят с {firstBusyDate:dd.MM.yyyy}");
        }
    }

    private async Task ValidateOptionsConflictsAsync(CreateBookingDto booking, int? currentBookingId, BookingValidationResult result)
    {
        var dateRange = Enumerable.Range(0, (booking.DepartureDate - booking.ArrivalDate).Days)
            .Select(offset => booking.ArrivalDate.AddDays(offset))
            .ToList();

        foreach (var date in dateRange)
        {
            // Проверяем сауну
            if (booking.NeedsSauna)
            {
                var saunaConflict = await context.Bookings
                    .Where(b => b.Id != (currentBookingId ?? 0))
                    .Where(b => date >= b.ArrivalDate.Date && date < b.DepartureDate.Date)
                    .Where(b => b.NeedsSauna)
                    .Select(b => new { b.Guest.FullName, b.ArrivalDate, b.DepartureDate })
                    .FirstOrDefaultAsync();

                if (saunaConflict != null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Сауна уже забронирована на {date:dd.MM.yyyy} " +
                                      $"(гость: {saunaConflict.FullName}, " +
                                      $"бронь: {saunaConflict.ArrivalDate:dd.MM}–{saunaConflict.DepartureDate:dd.MM.yyyy})");
                }
            }

            // Проверяем банкетный зал
            if (booking.NeedsBanquetHall)
            {
                var hallConflict = await context.Bookings
                    .Where(b => b.Id != (currentBookingId ?? 0))
                    .Where(b => date >= b.ArrivalDate.Date && date < b.DepartureDate.Date)
                    .Where(b => b.NeedsBanquetHall)
                    .Select(b => new { b.Guest.FullName, b.ArrivalDate, b.DepartureDate })
                    .FirstOrDefaultAsync();

                if (hallConflict != null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Банкетный зал уже забронирован на {date:dd.MM.yyyy} " +
                                      $"(гость: {hallConflict.FullName}, " +
                                      $"бронь: {hallConflict.ArrivalDate:dd.MM}–{hallConflict.DepartureDate:dd.MM.yyyy})");
                }
            }

            // Если уже нашли конфликт — прерываем
            if (!result.IsValid) break;
        }
    }

    private async Task<bool> CheckDateAvailabilityAsync(DateTime date, ReservedUnits unit, int? currentBookingId)
    {
        var query = context.Bookings
            .Where(b => b.Id != (currentBookingId ?? 0))
            .Where(b => date >= b.ArrivalDate.Date && date < b.DepartureDate.Date);

        return unit switch
        {
            ReservedUnits.WholeHouse => !await query.AnyAsync(),
            ReservedUnits.PondSide => !await query.AnyAsync(b =>
                b.ReservedUnit == ReservedUnits.PondSide ||
                b.ReservedUnit == ReservedUnits.WholeHouse),
            ReservedUnits.ParkingSide => !await query.AnyAsync(b =>
                b.ReservedUnit == ReservedUnits.ParkingSide ||
                b.ReservedUnit == ReservedUnits.WholeHouse),
            _ => true
        };
    }
}