namespace AgroAdmin.Shared.Dto.Telegram.Responses;

public class BookingSummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalBookings { get; set; }
    public int TotalGuests { get; set; }
    public decimal TotalRevenue { get; set; }
    public double OccupancyRate { get; set; }
    public double AvgStayLength { get; set; }
    public int CancelledBookings { get; set; }
    public int CompletedBookings { get; set; }
}