namespace AgroAdmin.Shared.Dto.Notifications;

public class TelegramRecipientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Role { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CommandCountToday { get; set; }
}