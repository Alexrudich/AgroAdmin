// Domain/Models/Guest.cs
using AgroAdmin.Domain.Models;

namespace AgroAdmin.Domain.Models;

public class Guest
{
    public int Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public string? Comment { get; private set; } // общие заметки о госте

    // Навигационное свойство для связи с бронями
    private readonly List<Booking> _bookings = new();
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    private Guest() { } // для EF Core

    public Guest(string fullName, string phone, string? comment = null)
    {
        FullName = fullName;
        Phone = phone;
        Comment = comment;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateInfo(string fullName, string phone, string? comment = null)
    {
        FullName = fullName;
        Phone = phone;
        Comment = comment;
    }
}