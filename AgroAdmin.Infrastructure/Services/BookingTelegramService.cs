using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Telegram.Responses;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.Infrastructure.Services;

public class BookingTelegramService(AppDbContext context, ILogger<BookingTelegramService> logger)
    : IBookingTelegramService
{
    public async Task<List<TelegramBookingDto>> GetNearestBookingsAsync(int days = 7)
    {
        try
        {
            var today = DateTime.Today;
            var endDate = today.AddDays(days);

            var bookingsData = await context.Bookings
                .Include(b => b.Guest)
                .Where(b => b.ArrivalDate >= today && b.ArrivalDate <= endDate)
                .OrderBy(b => b.ArrivalDate)
                .ThenBy(b => b.CheckInTime)
                .Take(15)
                .Select(b => new
                {
                    b.Id,
                    b.Guest,
                    b.ArrivalDate,
                    b.DepartureDate,
                    b.TotalGuestsCount,
                    b.ReservedUnit,
                    b.NeedsSauna,
                    b.AccommodationCost,
                    b.AdminNotes
                })
                .ToListAsync();

            return bookingsData.Select(b => new TelegramBookingDto
            {
                Id = b.Id,
                GuestName = b.Guest?.FullName ?? "Гость не указан",
                Phone = b.Guest?.Phone,
                ArrivalDate = b.ArrivalDate,
                DepartureDate = b.DepartureDate,
                TotalGuestsCount = b.TotalGuestsCount,
                UnitName = b.ReservedUnit.ToFriendlyString(),
                UnitEmoji = GetUnitEmoji(b.ReservedUnit),
                NeedsSauna = b.NeedsSauna,
                TotalPrice = b.AccommodationCost,
                Notes = b.AdminNotes
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetNearestBookingsAsync");
            return new List<TelegramBookingDto>();
        }
    }

    public async Task<List<DailyAvailability>> GetDailyAvailabilityAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var result = new List<DailyAvailability>();

            var bookings = await context.Bookings
                .Where(b => b.ArrivalDate <= endDate && b.DepartureDate >= startDate)
                .ToListAsync();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var daily = new DailyAvailability
                {
                    Date = date,
                    IsPondSideFree = true,
                    IsParkingSideFree = true,
                    IsWholeHouseFree = true
                };

                foreach (var booking in bookings)
                {
                    if (booking.ArrivalDate <= date && booking.DepartureDate > date)
                    {
                        switch (booking.ReservedUnit)
                        {
                            case ReservedUnits.PondSide:
                                daily.IsPondSideFree = false;
                                break;
                            case ReservedUnits.ParkingSide:
                                daily.IsParkingSideFree = false;
                                break;
                            case ReservedUnits.WholeHouse:
                                daily.IsWholeHouseFree = false;
                                break;
                        }
                    }
                }

                result.Add(daily);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetDailyAvailabilityAsync");
            return new List<DailyAvailability>();
        }
    }

    public async Task<BookingSummaryDto> GetBookingSummaryAsync(int year, int month)
    {
        // TODO: реализовать позже
        logger.LogWarning("GetBookingSummaryAsync not implemented yet");
        return new BookingSummaryDto
        {
            Year = year,
            Month = month,
            TotalBookings = 0,
            TotalGuests = 0,
            TotalRevenue = 0,
            OccupancyRate = 0,
            AvgStayLength = 0,
            CancelledBookings = 0,
            CompletedBookings = 0
        };
    }

    private static string GetUnitEmoji(ReservedUnits unit)
    {
        return unit switch
        {
            ReservedUnits.PondSide => "🌊",
            ReservedUnits.ParkingSide => "🚗",
            ReservedUnits.WholeHouse => "🏠",
            _ => "🏢"
        };
    }
}