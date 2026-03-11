namespace AgroAdmin.Shared.Dto
{
    public class BookingCapacityDto
    {
        public int MaxAdults { get; set; }
        public int MaxChildren { get; set; }
        public int MaxInfants { get; set; }
        public int TotalBeds { get; set; }
        public bool AllowsExtraMattress { get; set; }
    }
}