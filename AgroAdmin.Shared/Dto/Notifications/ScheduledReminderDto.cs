using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto.Notifications;

public class ScheduledReminderDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public bool IsSent { get; set; }
    public ReminderPriority Priority { get; set; }
    public string? TargetChatId { get; set; }
}