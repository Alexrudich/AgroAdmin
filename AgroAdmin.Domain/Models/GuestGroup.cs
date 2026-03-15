namespace AgroAdmin.Domain.Models;

public class GuestGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // например "Семья Петровых"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; } // опционально

    // Навигация
    public ICollection<GuestGroupMember> Members { get; set; } = new List<GuestGroupMember>();
}