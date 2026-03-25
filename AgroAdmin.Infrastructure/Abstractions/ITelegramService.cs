using AgroAdmin.Shared.Dto.Bookings.Responses;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface ITelegramService
{
    Task SendMessageAsync(string message, string? targetChatId = null);
    Task SendBookingNotificationAsync(BookingDto booking);
    Task StartReceivingAsync(CancellationToken cancellationToken);
    Task StopReceivingAsync();
}