namespace AgroAdmin.Shared.Dto.Telegram.Responses;

public class FreeSlotDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int AvailableUnits { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitEmoji { get; set; } = string.Empty;
}