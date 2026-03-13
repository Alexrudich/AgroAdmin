// AgroAdmin.Shared/Dto/CreateBookingDto.cs
using AgroAdmin.Shared.Dto.Guests;
using AgroAdmin.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace AgroAdmin.Shared.Dto.Bookings;

public class CreateBookingDto
{
    public GuestDto? Guest { get; set; }

    [Required]
    public DateTime ArrivalDate { get; set; } = DateTime.Today;

    [Required]
    public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);

    public ReservedUnits ReservedUnit { get; set; }

    public bool NeedsSauna { get; set; }
    public bool NeedsBanquetHall { get; set; }

    [Range(1, 20, ErrorMessage = "Минимум 1 гость")]
    public int TotalGuestsCount { get; set; } = 1;

    public int AdultsCount { get; set; } = 1;
    public int ChildrenCount { get; set; }
    public int InfantsCount { get; set; }

    public bool HasDog { get; set; }
    public bool IsFirstTimeGuest { get; set; } = true;
    public string? AdminNotes { get; set; }
    public string? FeedbackComment { get; set; }
}