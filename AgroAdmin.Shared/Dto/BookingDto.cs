using System.ComponentModel.DataAnnotations;
using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto;

public class BookingDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите имя гостя")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя слишком короткое")]
    public string GuestName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Неверный формат телефона")]
    public string? GuestPhone { get; set; }

    [Required]
    public DateTime ArrivalDate { get; set; } = DateTime.Today;

    [Required]
    public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);

    public ReservedUnits ReservedUnit { get; set; }

    [Range(1, 20, ErrorMessage = "Минимум 1 гость")]
    public int TotalGuestsCount { get; set; } = 1;

    public int AdultsCount { get; set; } = 1;
    public int ChildrenCount { get; set; }
    public int InfantsCount { get; set; }

    public bool HasDog { get; set; }
    public bool IsFirstTimeGuest { get; set; } = true;
    public string? AdminNotes { get; set; }
}