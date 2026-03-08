using AgroAdmin.Shared.Dto;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface ITelegramService
{
    Task SendMessageAsync(string message);
    Task SendBookingNotificationAsync(CreateBookingDto booking);
}