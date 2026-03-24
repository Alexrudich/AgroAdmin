namespace AgroAdmin.Domain.Models;

public class Guest
{
    public int Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public string? Comment { get; private set; }

    private readonly List<Booking> _bookings = [];
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    private readonly List<GuestGroupMember> _groupMembers = [];
    public IReadOnlyCollection<GuestGroupMember> GroupMembers => _groupMembers.AsReadOnly();

    private Guest() { }

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