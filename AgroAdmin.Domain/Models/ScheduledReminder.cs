using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Domain.Models;

public class ScheduledReminder
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public bool IsSent { get; set; } = false;

    public string? TargetChatId { get; set; } // Если пусто — шлем всем админам из конфига

    public ReminderPriority Priority { get; set; } = ReminderPriority.Normal;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}