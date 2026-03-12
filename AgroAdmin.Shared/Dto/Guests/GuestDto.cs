using System.ComponentModel.DataAnnotations;

namespace AgroAdmin.Shared.Dto.Guests;

public class GuestDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите имя гостя")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя слишком короткое")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите телефон")]
    [Phone(ErrorMessage = "Неверный формат телефона")]
    public string Phone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    [StringLength(500, ErrorMessage = "Комментарий слишком длинный")]
    public string? Comment { get; set; }

    // Вычисляемые поля (заполняются на основе связанных броней)
    public int TotalStays { get; set; }
    public DateTime? LastBookingDate { get; set; }
    public string? LastFeedback { get; set; }
}