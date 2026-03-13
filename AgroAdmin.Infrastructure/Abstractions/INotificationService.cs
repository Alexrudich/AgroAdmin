using AgroAdmin.Shared.Dto.Notifications;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface INotificationService
{
    Task<List<ScheduledReminderDto>> GetActiveRemindersAsync();
    Task<ScheduledReminderDto?> GetByIdAsync(int id);
    Task CreateReminderAsync(CreateReminderDto dto);
    Task UpdateReminderAsync(int id, CreateReminderDto dto);
    Task DeleteReminderAsync(int id);

    Task<List<TelegramRecipientDto>> GetRecipientsAsync();
    Task AddRecipientAsync(TelegramRecipientDto dto);
    Task DeleteRecipientAsync(int id);
    Task MarkAsSentAsync(int id);
}