using AgroAdmin.Shared.Dto.Guests;
using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto.Bookings.Responses;

public class BookingDto
{
    public int Id { get; set; }
    public GuestDto? Guest { get; set; }
    public DateTime ArrivalDate { get; set; }
    public DateTime DepartureDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReservedUnits ReservedUnit { get; set; }
    public bool NeedsSauna { get; set; }
    public bool NeedsBanquetHall { get; set; }
    public int TotalGuestsCount { get; set; }
    public int AdultsCount { get; set; }
    public int ChildrenCount { get; set; }
    public int InfantsCount { get; set; }
    public bool HasDog { get; set; }
    public bool IsFirstTimeGuest { get; set; }
    public string? AdminNotes { get; set; }
    public string? FeedbackComment { get; set; }
    public decimal? AccommodationCost { get; set; }
    public TimeSpan CheckInTime { get; set; }
}