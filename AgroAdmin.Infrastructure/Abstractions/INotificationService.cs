using AgroAdmin.Domain.Models;
using AgroAdmin.Shared.Dto.Notifications;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface INotificationService
{
    Task CreateReminderAsync(CreateReminderDto dto);
    Task<List<ScheduledReminderDto>> GetActiveRemindersAsync();
}