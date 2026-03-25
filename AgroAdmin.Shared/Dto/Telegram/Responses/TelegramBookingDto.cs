namespace AgroAdmin.Shared.Dto.Telegram.Responses;

public class TelegramBookingDto
{
    public int Id { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime ArrivalDate { get; set; }
    public DateTime DepartureDate { get; set; }
    public int TotalGuestsCount { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitEmoji { get; set; } = string.Empty;
    public bool NeedsSauna { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Notes { get; set; }
}