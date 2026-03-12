using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto.Bookings;
public record BookingCreatedEvent
{
    public int BookingId { get; init; }
    public string GuestName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public DateTime ArrivalDate { get; init; }
    public DateTime DepartureDate { get; init; }
    public ReservedUnits Unit { get; init; }
    public bool NeedsSauna { get; init; }
    public string? AdminNotes { get; init; }
}