using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto;

public class BookingDto
{
    public int Id { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? GuestPhone { get; set; }
    public DateTime ArrivalDate { get; set; } = DateTime.Today;
    public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);
    public ReservedUnits ReservedUnit { get; set; }

    public int TotalGuestsCount { get; set; } = 1;
    public int AdultsCount { get; set; } = 1;
    public int ChildrenCount { get; set; }
    public int InfantsCount { get; set; }

    public bool HasDog { get; set; }
    public bool IsFirstTimeGuest { get; set; } = true;
    public string? AdminNotes { get; set; }
}
