namespace AgroAdmin.Domain.Models;

public class TelegramRecipient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? Role { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastCommandAt { get; set; }
    public int CommandCountToday { get; set; }
}