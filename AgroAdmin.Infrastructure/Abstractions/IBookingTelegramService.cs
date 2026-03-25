using AgroAdmin.Shared.Dto.Telegram.Responses;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface IBookingTelegramService
{
    Task<List<TelegramBookingDto>> GetNearestBookingsAsync(int days = 7);
    Task<List<DailyAvailability>> GetDailyAvailabilityAsync(DateTime startDate, DateTime endDate);
    Task<BookingSummaryDto> GetBookingSummaryAsync(DateTime startDate, DateTime endDate);
}