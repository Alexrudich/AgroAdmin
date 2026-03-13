namespace AgroAdmin.Shared.Dto.Notifications;

public class TelegramRecipientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}