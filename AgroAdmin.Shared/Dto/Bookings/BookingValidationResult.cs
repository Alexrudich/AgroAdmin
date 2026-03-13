namespace AgroAdmin.Shared.Dto.Bookings
{
    public class BookingValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime? EarliestAvailableDate { get; set; }
        public DateTime? LatestAvailableDate { get; set; }
    }
}