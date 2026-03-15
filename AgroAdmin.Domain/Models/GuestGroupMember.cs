namespace AgroAdmin.Domain.Models;

public class GuestGroupMember
{
    public int GuestId { get; set; }
    public int GroupId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Навигация
    public Guest Guest { get; set; } = null!;
    public GuestGroup Group { get; set; } = null!;
}