using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto.Notifications;

public class CreateReminderDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow.AddHours(1);
    public ReminderPriority Priority { get; set; } = ReminderPriority.Normal;
    public string? TargetChatId { get; set; } // Если пусто — всем админам
}