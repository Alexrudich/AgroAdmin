using AgroAdmin.Shared.Dto.Telegram.Responses;

namespace AgroAdmin.Infrastructure.Abstractions
{
    public interface ITelegramApiClient
    {
        Task<List<TelegramBookingDto>> GetNearestBookingsAsync(int days);
        Task<List<DailyAvailability>> GetAvailabilityAsync(DateTime startDate, DateTime endDate);
    }
}
