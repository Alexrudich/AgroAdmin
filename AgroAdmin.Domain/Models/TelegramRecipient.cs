namespace AgroAdmin.Domain.Models;

public class TelegramRecipient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}