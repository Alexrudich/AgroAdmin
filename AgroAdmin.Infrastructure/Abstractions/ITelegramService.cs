using AgroAdmin.Shared.Dto.Bookings;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface ITelegramService
{
    Task SendMessageAsync(string message, string? targetChatId = null);
    Task SendBookingNotificationAsync(BookingDto booking);
}